using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.UI.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace MBBSEmu.UI.Main
{
    public enum EnumAdminConsoleOutcome
    {
        /// <summary>
        ///     The sysop (or a signal) shut MBBSEmu down and services stopped
        /// </summary>
        Shutdown,

        /// <summary>
        ///     Starting services threw; the exception is in AdminConsoleResult.Exception
        /// </summary>
        StartupFailed,

        /// <summary>
        ///     The UI failed while running; services were stopped
        /// </summary>
        UiFailed,

        /// <summary>
        ///     Shutdown did not finish and the process was told to exit
        /// </summary>
        ForceQuit,

        /// <summary>
        ///     Terminal.Gui could not start; nothing was started
        /// </summary>
        TerminalUnavailable
    }

    public sealed record AdminConsoleResult(EnumAdminConsoleOutcome Outcome, Exception Exception);

    public sealed class AdminConsoleOptions
    {
        public MessageLogger MessageLogger { get; init; }
        public AuditLogger AuditLogger { get; init; }

        /// <summary>
        ///     Current user sessions; called on the UI thread every refresh
        /// </summary>
        public Func<IReadOnlyList<ChannelSession>> GetSessions { get; init; }

        public int ChannelCount { get; init; }

        /// <summary>
        ///     Service state and build for the status bar, from StatusLine.RightText
        /// </summary>
        public string StatusRightText { get; init; }

        public string Build { get; init; }

        /// <summary>
        ///     Logged as a warning once the UI is up, or null for none
        /// </summary>
        public string StartupWarning { get; init; }

        public Func<IApplication> CreateApplication { get; init; } = () =>
        {
            var app = Application.Create();
            app.Init(null);
            return app;
        };

        /// <summary>
        ///     Dialogs for the console, or null for the Terminal.Gui ones
        /// </summary>
        public Func<IApplication, IAdminConsoleDialogs> CreateDialogs { get; init; }

        public Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

        public Action<int> ExitProcess { get; init; } = Environment.Exit;
    }

    /// <summary>
    ///     Runs the Admin Console: owns the terminal on the calling (main) thread, starts and stops services
    ///     around it, and restores the terminal however it ends
    /// </summary>
    public sealed class AdminConsole
    {
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

        private readonly AdminConsoleOptions _options;

        private IApplication _app;
        private AdminConsoleView _view;
        private IAdminConsoleDialogs _dialogs;
        private UiLogCapture _capture;
        private ShutdownCoordinator _coordinator;
        private Task _startTask;

        private volatile Exception _startException;
        private Exception _uiException;
        private string _forceQuitMessage;
        private bool _ending;

        public AdminConsole(AdminConsoleOptions options)
        {
            _options = options;
        }

        /// <summary>
        ///     Shows the console, starts services in the background, and blocks until MBBSEmu shuts down
        /// </summary>
        /// <param name="startServices">Starts the host and servers; runs on a background thread</param>
        /// <param name="stopServices">Stops everything startServices started; runs on a background thread</param>
        public AdminConsoleResult Run(Action startServices, Action stopServices)
        {
            try
            {
                _app = _options.CreateApplication();
            }
            catch (Exception e)
            {
                return new AdminConsoleResult(EnumAdminConsoleOutcome.TerminalUnavailable, e);
            }

            ConsoleTheme.ApplyGlobalDefaults();
            ConsoleOwnership.SetUiActive(true);
            var signals = new List<PosixSignalRegistration>();
            try
            {
                _capture = new UiLogCapture(_options.MessageLogger, _options.AuditLogger);
                if (!string.IsNullOrEmpty(_options.StartupWarning))
                    _options.MessageLogger.Warn(_options.StartupWarning);

                _dialogs = _options.CreateDialogs?.Invoke(_app) ?? new TerminalGuiAdminConsoleDialogs(_app, _options.Build);
                _coordinator = new ShutdownCoordinator(() =>
                {
                    WaitForStartTask();
                    stopServices();
                }, _options.UtcNow);

                _view = new AdminConsoleView(_dialogs, _options.StatusRightText);
                _view.ShutdownRequested += (_, _) => RequestShutdown(unattended: false);
                _app.ScreenChanged += (_, e) => _view.ApplyScreenSize(e.Value.Width, e.Value.Height);
                _view.ApplyScreenSize(_app.Screen.Width, _app.Screen.Height);
                _app.Keyboard.KeyDown += OnKeyDown;

                RegisterSignals(signals);
                _app.AddTimeout(RefreshInterval, Refresh);

                //Module loading runs for a while; give it its own thread rather than a thread-pool one
                _startTask = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        startServices();
                    }
                    catch (Exception e)
                    {
                        _startException = e;
                        _app.Invoke(EndLoop);
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                try
                {
                    _app.Run(_view, e =>
                    {
                        _uiException ??= e;
                        return false;
                    });
                }
                catch (Exception e)
                {
                    _uiException ??= e;
                }
            }
            catch (Exception e)
            {
                //Setting up the UI failed before it ran
                _uiException ??= e;
            }
            finally
            {
                foreach (var signal in signals)
                    signal.Dispose();

                //Restores the terminal before anything else is written to it
                _app.Dispose();
                _capture?.Dispose();
                ConsoleOwnership.SetUiActive(false);
            }

            return Finish();
        }

        /// <summary>
        ///     Starts a shutdown. Attended requests (Ctrl+Q, the menu) ask first; unattended ones (signals) don't
        ///     and give up after ShutdownCoordinator.UnattendedTimeout. A second attended request while shutting
        ///     down offers to force quit. Must be called on the UI thread.
        /// </summary>
        internal void RequestShutdown(bool unattended)
        {
            switch (_coordinator.Phase)
            {
                case EnumShutdownPhase.Running:
                    if (!unattended && !_dialogs.ConfirmShutdown())
                        return;

                    if (_coordinator.Begin(unattended))
                        _view.SetShuttingDown();
                    break;

                case EnumShutdownPhase.ShuttingDown when unattended:
                    _coordinator.Begin(unattended: true);
                    break;

                case EnumShutdownPhase.ShuttingDown:
                    if (_dialogs.ConfirmForceQuit())
                        ForceExit(AdminConsoleText.ForceQuitExit);
                    break;
            }
        }

        private AdminConsoleResult Finish()
        {
            if (_forceQuitMessage != null)
            {
                Console.WriteLine(_forceQuitMessage);
                _options.ExitProcess(1);
                return new AdminConsoleResult(EnumAdminConsoleOutcome.ForceQuit, null);
            }

            if (_uiException != null)
                return StopAfterUiFailure(_uiException);

            if (_startException != null)
                return new AdminConsoleResult(EnumAdminConsoleOutcome.StartupFailed, _startException);

            //The loop only ends on its own once services have stopped; anything else means services are still up
            if (_coordinator.Phase != EnumShutdownPhase.Stopped)
                return StopAfterUiFailure(new InvalidOperationException(AdminConsoleText.UnexpectedClose));

            return new AdminConsoleResult(EnumAdminConsoleOutcome.Shutdown, _coordinator.StopException);
        }

        /// <summary>
        ///     A UI failure is a bug to fix, so MBBSEmu shuts down rather than running on without the console
        /// </summary>
        private AdminConsoleResult StopAfterUiFailure(Exception failure)
        {
            _options.MessageLogger.Log(LogLevel.Critical, AdminConsoleText.UiFailed, failure);

            if (_coordinator != null)
            {
                _coordinator.Begin(unattended: true);
                while (_coordinator.Phase != EnumShutdownPhase.Stopped && !_coordinator.DeadlineExpired)
                    Thread.Sleep(50);

                if (_coordinator.Phase != EnumShutdownPhase.Stopped)
                {
                    Console.WriteLine(AdminConsoleText.UnattendedTimeoutExit);
                    _options.ExitProcess(1);
                }
            }

            return new AdminConsoleResult(EnumAdminConsoleOutcome.UiFailed, failure);
        }

        /// <summary>
        ///     Ends the UI loop; Run then restores the terminal, prints message and exits the process
        /// </summary>
        private void ForceExit(string message)
        {
            _forceQuitMessage ??= message;
            EndLoop();
        }

        /// <summary>
        ///     Ends the UI loop even if a dialog is open: a stop request on the console only takes effect once every
        ///     dialog above it has closed, and after a signal nobody is there to close them. The refresh timer keeps
        ///     calling StopTopmost until the console itself has stopped.
        /// </summary>
        private void EndLoop()
        {
            _ending = true;
            StopTopmost();
        }

        private void StopTopmost()
        {
            if (_app.TopRunnable != null && !ReferenceEquals(_app.TopRunnableView, _view))
                _app.RequestStop(_app.TopRunnable);
            else
                _app.RequestStop(_view);
        }

        private bool Refresh()
        {
            if (_ending)
            {
                StopTopmost();
                return true;
            }

            try
            {
                var sessions = _options.GetSessions();
                _view.Update(
                    _capture.DrainApplicationLog(),
                    _capture.DrainAuditLog(),
                    ChannelList.Build(_options.ChannelCount, sessions),
                    ChannelList.CountOnline(_options.ChannelCount, sessions));

                if (_startException != null || _coordinator.Phase == EnumShutdownPhase.Stopped)
                    EndLoop();
                else if (_coordinator.DeadlineExpired)
                    ForceExit(AdminConsoleText.UnattendedTimeoutExit);

                return true;
            }
            catch (Exception e)
            {
                _uiException ??= e;
                EndLoop();
                return true;
            }
        }

        private void OnKeyDown(object sender, Key key)
        {
            switch (AdminConsoleKeys.Decide(key, consoleOnTop: _app.TopRunnableView == _view))
            {
                case EnumConsoleKeyAction.Swallow:
                    key.Handled = true;
                    break;
                case EnumConsoleKeyAction.Shutdown:
                    key.Handled = true;
                    RequestShutdown(unattended: false);
                    break;
            }
        }

        /// <summary>
        ///     SIGINT, SIGTERM and SIGHUP (e.g. an SSH session dropping) shut down without asking
        /// </summary>
        private void RegisterSignals(List<PosixSignalRegistration> signals)
        {
            foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP })
            {
                try
                {
                    signals.Add(PosixSignalRegistration.Create(signal, context =>
                    {
                        context.Cancel = true;
                        try
                        {
                            _app.Invoke(() => RequestShutdown(unattended: true));
                        }
                        catch (ObjectDisposedException)
                        {
                            //Signal arrived while the console was closing
                        }
                    }));
                }
                catch (PlatformNotSupportedException)
                {
                    //Not every signal exists on every platform
                }
            }
        }

        private void WaitForStartTask()
        {
            try
            {
                _startTask?.Wait();
            }
            catch (AggregateException)
            {
                //Reported separately as a startup failure
            }
        }
    }
}
