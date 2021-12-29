using NLog;
using System;
using System.Runtime.InteropServices;

namespace MBBSEmu.DOS
{
    /// <summary>
    ///     Enables VT100 Terminal Emulation with Command Console by calling SetConsoleMode through an external call to the Kernel
    ///
    ///     This will only work on WIN32 Environments
    ///
    ///     More Information: https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN
    /// </summary>
    internal class Win32VT100
    {
        private const int STD_INPUT_HANDLE = -10;

        private const int STD_OUTPUT_HANDLE = -11;

        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        private readonly ILogger _logger;

        public Win32VT100(ILogger logger)
        {
            _logger = logger;
        }

        public void Enable()
        {
            try
            {
                var iStdIn = GetStdHandle(STD_INPUT_HANDLE);
                var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);

                if (!GetConsoleMode(iStdIn, out var inConsoleMode))
                    throw new Exception("Failed to get Input Console Mode");

                if (!GetConsoleMode(iStdOut, out var outConsoleMode))
                    throw new Exception("Failed to get Output Console Mode");

                inConsoleMode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
                outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

                if (!SetConsoleMode(iStdIn, inConsoleMode))
                    throw new Exception($"Failed to set Input Console Mode, Error Code: {GetLastError()}");

                if (!SetConsoleMode(iStdOut, outConsoleMode))
                    throw new Exception($"Failed to set Output Console Mode, Error Code: {GetLastError()}");
            }
            catch (Exception e)
            {
                _logger.Error(e);
                _logger.Error("VT100 Emulation is not enabled, and displaying ANSI characters might not work properly.");
            }
        }
    }
}
