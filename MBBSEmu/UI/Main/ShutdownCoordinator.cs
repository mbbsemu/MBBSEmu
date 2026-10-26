using System;
using System.Threading.Tasks;

namespace MBBSEmu.UI.Main
{
    public enum EnumShutdownPhase
    {
        Running,
        ShuttingDown,
        Stopped
    }

    /// <summary>
    ///     Stops MBBSEmu's services off the UI thread, so the Admin Console keeps drawing (and showing shutdown
    ///     log lines) while modules wind down
    ///
    ///     An unattended shutdown (a signal, or a UI failure) has a deadline: a module stuck in a loop can block
    ///     MbbsHost.WaitForShutdown() forever, and with nobody at the keyboard the process would otherwise hang.
    /// </summary>
    public sealed class ShutdownCoordinator
    {
        public static readonly TimeSpan UnattendedTimeout = TimeSpan.FromSeconds(15);

        private readonly Action _stopServices;
        private readonly Func<DateTime> _utcNow;
        private readonly object _lock = new();

        private EnumShutdownPhase _phase = EnumShutdownPhase.Running;
        private DateTime? _deadline;
        private Exception _stopException;

        public ShutdownCoordinator(Action stopServices, Func<DateTime> utcNow)
        {
            _stopServices = stopServices;
            _utcNow = utcNow;
        }

        public EnumShutdownPhase Phase
        {
            get { lock (_lock) return _phase; }
        }

        /// <summary>
        ///     Exception thrown by the stop action, if any
        /// </summary>
        public Exception StopException
        {
            get { lock (_lock) return _stopException; }
        }

        /// <summary>
        ///     True once an unattended shutdown has run past UnattendedTimeout without stopping
        /// </summary>
        public bool DeadlineExpired
        {
            get
            {
                lock (_lock)
                    return _phase == EnumShutdownPhase.ShuttingDown && _deadline.HasValue && _utcNow() >= _deadline.Value;
            }
        }

        /// <summary>
        ///     Starts stopping services in the background
        /// </summary>
        /// <param name="unattended">True when nobody confirmed the shutdown; sets the deadline if none is set yet</param>
        /// <returns>True if this call started the shutdown, false if one was already under way or finished</returns>
        public bool Begin(bool unattended)
        {
            lock (_lock)
            {
                if (unattended && _phase != EnumShutdownPhase.Stopped)
                    _deadline ??= _utcNow() + UnattendedTimeout;

                if (_phase != EnumShutdownPhase.Running)
                    return false;

                _phase = EnumShutdownPhase.ShuttingDown;
            }

            Task.Run(StopServices);
            return true;
        }

        private void StopServices()
        {
            try
            {
                _stopServices();
            }
            catch (Exception e)
            {
                lock (_lock)
                    _stopException = e;
            }
            finally
            {
                lock (_lock)
                    _phase = EnumShutdownPhase.Stopped;
            }
        }
    }
}
