using FluentAssertions;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.UI.Main;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    [Collection("TerminalGui")]
    public class AdminConsole_Tests
    {
        private sealed class FakeDialogs : IAdminConsoleDialogs
        {
            public readonly ConcurrentQueue<bool> ShutdownAnswers = new();
            public readonly ConcurrentQueue<bool> ForceQuitAnswers = new();
            public int ShutdownAsks;
            public int ForceQuitAsks;

            public bool ConfirmShutdown()
            {
                Interlocked.Increment(ref ShutdownAsks);
                return ShutdownAnswers.TryDequeue(out var answer) && answer;
            }

            public bool ConfirmForceQuit()
            {
                Interlocked.Increment(ref ForceQuitAsks);
                return ForceQuitAnswers.TryDequeue(out var answer) && answer;
            }

            public void ShowEntry(string title, string text) { }

            public void ShowAbout() { }
        }

        private static readonly DateTime StartTime = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        private readonly FakeDialogs _dialogs = new();
        private readonly MessageLogger _messageLogger = new(new ConsoleTarget());
        private readonly AuditLogger _auditLogger = new(new ConsoleTarget());
        private readonly List<int> _exitCodes = new();
        private readonly List<(int DelayMs, Action<IApplication> Action)> _script = new();
        private Func<IReadOnlyList<ChannelSession>> _getSessions = Array.Empty<ChannelSession>;
        private long _clockTicks = StartTime.Ticks;
        private AdminConsole _console;

        private int _starts;
        private int _stops;

        /// <summary>
        ///     Schedules an action on the UI thread, delayMs after the console starts
        /// </summary>
        private void At(int delayMs, Action<IApplication> action) => _script.Add((delayMs, action));

        private void KeyAt(int delayMs, Key key) => At(delayMs, app => app.InjectKey(key));

        private void Start() => Interlocked.Increment(ref _starts);

        private void Stop() => Interlocked.Increment(ref _stops);

        private AdminConsoleResult Run(Action startServices, Action stopServices, string startupWarning = null,
            bool realDialogs = false)
        {
            _console = new AdminConsole(new AdminConsoleOptions
            {
                MessageLogger = _messageLogger,
                AuditLogger = _auditLogger,
                GetSessions = () => _getSessions(),
                ChannelCount = 4,
                StatusRightText = StatusLine.RightText(true, false, "test"),
                Build = "test",
                StartupWarning = startupWarning,
                CreateApplication = () =>
                {
                    var app = Application.Create();
                    app.Init(DriverRegistry.Names.ANSI);
                    app.Driver!.SetScreenSize(80, 24);
                    foreach (var (delayMs, action) in _script)
                        app.AddTimeout(TimeSpan.FromMilliseconds(delayMs), () =>
                        {
                            action(app);
                            return false;
                        });
                    return app;
                },
                CreateDialogs = app => realDialogs ? new TerminalGuiAdminConsoleDialogs(app, "test") : _dialogs,
                UtcNow = () => new DateTime(Interlocked.Read(ref _clockTicks), DateTimeKind.Utc),
                ExitProcess = code =>
                {
                    lock (_exitCodes)
                        _exitCodes.Add(code);
                }
            });

            //Its own thread, so a busy thread pool (the full suite runs in parallel) can't delay the console starting
            var run = Task.Factory.StartNew(() => _console.Run(startServices, stopServices), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
            run.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the admin console should exit");
            return run.Result;
        }

        [Fact]
        public void CtrlQConfirmed_StopsServicesAndReturnsShutdown()
        {
            _dialogs.ShutdownAnswers.Enqueue(true);
            KeyAt(100, Key.Q.WithCtrl);

            var result = Run(Start, Stop);

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.Shutdown);
            _starts.Should().Be(1);
            _stops.Should().Be(1);
            _exitCodes.Should().BeEmpty();
        }

        [Fact]
        public void CtrlQDeclined_KeepsRunning()
        {
            _dialogs.ShutdownAnswers.Enqueue(false);
            _dialogs.ShutdownAnswers.Enqueue(true);
            var stopsAfterDecline = -1;
            KeyAt(100, Key.Q.WithCtrl);
            At(400, _ => stopsAfterDecline = _stops);
            KeyAt(500, Key.Q.WithCtrl);

            var result = Run(Start, Stop);

            stopsAfterDecline.Should().Be(0);
            _dialogs.ShutdownAsks.Should().Be(2);
            result.Outcome.Should().Be(EnumAdminConsoleOutcome.Shutdown);
        }

        [Fact]
        public void Esc_DoesNotStopTheConsole()
        {
            _dialogs.ShutdownAnswers.Enqueue(true);
            var asksAfterEsc = -1;
            var stopsAfterEsc = -1;
            KeyAt(100, Key.Esc);
            At(400, _ =>
            {
                asksAfterEsc = _dialogs.ShutdownAsks;
                stopsAfterEsc = _stops;
            });
            KeyAt(500, Key.Q.WithCtrl);

            var result = Run(Start, Stop);

            asksAfterEsc.Should().Be(0);
            stopsAfterEsc.Should().Be(0);
            result.Outcome.Should().Be(EnumAdminConsoleOutcome.Shutdown);
        }

        [Fact]
        public void CtrlQDuringStartup_StopsAfterStartFinishes()
        {
            using var startGate = new ManualResetEventSlim();
            var startFinished = false;
            var stopSawStartFinished = false;
            _dialogs.ShutdownAnswers.Enqueue(true);
            KeyAt(100, Key.Q.WithCtrl);
            At(300, _ => startGate.Set());

            var result = Run(
                () =>
                {
                    startGate.Wait(TimeSpan.FromSeconds(5));
                    Volatile.Write(ref startFinished, true);
                },
                () =>
                {
                    stopSawStartFinished = Volatile.Read(ref startFinished);
                    Stop();
                });

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.Shutdown);
            stopSawStartFinished.Should().BeTrue();
            _stops.Should().Be(1);
        }

        [Fact]
        public void SecondCtrlQDuringShutdown_AsksForceQuit()
        {
            using var stopGate = new ManualResetEventSlim();
            _dialogs.ShutdownAnswers.Enqueue(true);
            _dialogs.ForceQuitAnswers.Enqueue(true);
            KeyAt(100, Key.Q.WithCtrl);
            KeyAt(300, Key.Q.WithCtrl);

            var result = Run(Start, () => stopGate.Wait(TimeSpan.FromSeconds(10)));
            stopGate.Set();

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.ForceQuit);
            _dialogs.ForceQuitAsks.Should().Be(1);
            _exitCodes.Should().Equal(1);
        }

        [Fact]
        public void UnattendedShutdown_StuckStop_ForceQuitsAfterDeadline()
        {
            using var stopGate = new ManualResetEventSlim();
            At(100, _ => _console.RequestShutdown(unattended: true));
            At(300, _ => Interlocked.Add(ref _clockTicks, TimeSpan.FromSeconds(16).Ticks));

            var result = Run(Start, () => stopGate.Wait(TimeSpan.FromSeconds(10)));
            stopGate.Set();

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.ForceQuit);
            _dialogs.ShutdownAsks.Should().Be(0);
            _exitCodes.Should().Equal(1);
        }

        /// <summary>
        ///     A signal (e.g. an SSH drop) while a dialog is up: nobody is there to close it, so the console must
        /// </summary>
        [Fact]
        public void UnattendedShutdownWhileDialogOpen_Exits()
        {
            KeyAt(100, Key.Q.WithCtrl);
            At(400, _ => _console.RequestShutdown(unattended: true));

            var result = Run(Start, Stop, realDialogs: true);

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.Shutdown);
            _stops.Should().Be(1);
        }

        [Fact]
        public void UnattendedDeadlineWhileDialogOpen_ForceQuits()
        {
            using var stopGate = new ManualResetEventSlim();
            KeyAt(100, Key.Q.WithCtrl);
            At(400, _ => _console.RequestShutdown(unattended: true));
            At(600, _ => Interlocked.Add(ref _clockTicks, TimeSpan.FromSeconds(16).Ticks));

            var result = Run(Start, () => stopGate.Wait(TimeSpan.FromSeconds(10)), realDialogs: true);
            stopGate.Set();

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.ForceQuit);
            _exitCodes.Should().Equal(1);
        }

        [Fact]
        public void StartupThrows_ReturnsStartupFailed()
        {
            var result = Run(() => throw new InvalidOperationException("boom"), Stop);

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.StartupFailed);
            result.Exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("boom");
        }

        [Fact]
        public void TimerFailure_ReturnsUiFailedAndStopsServices()
        {
            var calls = 0;
            _getSessions = () =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                    throw new InvalidOperationException("ui");

                return Array.Empty<ChannelSession>();
            };

            var result = Run(Start, Stop);

            result.Outcome.Should().Be(EnumAdminConsoleOutcome.UiFailed);
            result.Exception.Message.Should().Be("ui");
            _stops.Should().Be(1);
        }

        [Fact]
        public void LogsGoToUiDuringRunAndConsoleAfter()
        {
            var consoleTargetDuringRun = true;
            var uiActiveDuringRun = false;
            _dialogs.ShutdownAnswers.Enqueue(true);
            At(100, _ =>
            {
                consoleTargetDuringRun = _messageLogger.HasTarget<ConsoleTarget>();
                uiActiveDuringRun = ConsoleOwnership.IsUiActive;
            });
            KeyAt(200, Key.Q.WithCtrl);

            Run(Start, Stop);

            consoleTargetDuringRun.Should().BeFalse();
            uiActiveDuringRun.Should().BeTrue();
            _messageLogger.HasTarget<ConsoleTarget>().Should().BeTrue();
            ConsoleOwnership.IsUiActive.Should().BeFalse();
        }

        [Fact]
        public void StartupWarning_IsLoggedToApplicationLog()
        {
            var queue = new QueueTarget();
            _messageLogger.AddTarget(queue);
            _dialogs.ShutdownAnswers.Enqueue(true);
            KeyAt(100, Key.Q.WithCtrl);

            Run(Start, Stop, startupWarning: "W");

            queue.DequeueAll().Should().Contain(x => x[2] is LogLevel && (LogLevel)x[2] == LogLevel.Warning && (string)x[3] == "W");
        }
    }
}
