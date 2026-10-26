using FluentAssertions;
using MBBSEmu.IO;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.IO
{
    /// <summary>
    ///     Shares the TerminalGui collection because it flips ConsoleOwnership, which the admin console tests also use
    /// </summary>
    [Collection("TerminalGui")]
    public class UiSafeConsoleWriter_Tests
    {
        private readonly StringWriter _console = new();
        private readonly List<string> _logged = new();

        private UiSafeConsoleWriter Create() => new(() => _console, _logged.Add);

        [Fact]
        public void WhileUiActive_WritesGoToTheLogNotTheConsole()
        {
            var writer = Create();
            ConsoleOwnership.SetUiActive(true);
            try
            {
                writer.Write("Assertion failed\n");
            }
            finally
            {
                ConsoleOwnership.SetUiActive(false);
            }

            _logged.Should().Equal("Assertion failed");
            _console.ToString().Should().BeEmpty();
        }

        [Fact]
        public void WithoutUi_WritesGoToTheConsole()
        {
            var writer = Create();

            writer.Write("Assertion failed\n");

            _console.ToString().Should().Be("Assertion failed\n");
            _logged.Should().BeEmpty();
        }

        /// <summary>
        ///     The writer is created while modules load, before the UI starts, so it must decide on every write
        /// </summary>
        [Fact]
        public void CreatedBeforeUiStarts_StillRoutesToTheLog()
        {
            var writer = Create();
            writer.Write("before\n");

            ConsoleOwnership.SetUiActive(true);
            try
            {
                writer.Write("during\n");
            }
            finally
            {
                ConsoleOwnership.SetUiActive(false);
            }

            _console.ToString().Should().Be("before\n");
            _logged.Should().Equal("during");
        }

        [Fact]
        public void Dispose_DoesNotCloseTheConsole()
        {
            var writer = Create();

            writer.Dispose();
            _console.Write("still open");

            _console.ToString().Should().Be("still open");
        }
    }
}
