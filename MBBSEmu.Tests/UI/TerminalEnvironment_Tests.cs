using FluentAssertions;
using MBBSEmu.UI;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.UI
{
    public class TerminalEnvironment_Tests
    {
        /// <summary>
        ///     Builds an environment lookup from "NAME=value;NAME=value"
        /// </summary>
        private static System.Func<string, string> Environment(string variables)
        {
            var values = new Dictionary<string, string>();
            foreach (var pair in variables.Split(';').Where(x => x.Length > 0))
            {
                var separator = pair.IndexOf('=');
                values[pair[..separator]] = pair[(separator + 1)..];
            }

            return name => values.TryGetValue(name, out var value) ? value : null;
        }

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        public void IsInteractive(bool inputRedirected, bool outputRedirected, bool expected)
        {
            TerminalEnvironment.IsInteractive(inputRedirected, outputRedirected).Should().Be(expected);
        }

        [Fact]
        public void IsUtf8Locale_Windows_AlwaysTrue()
        {
            TerminalEnvironment.IsUtf8Locale(isWindows: true, Environment("LANG=C")).Should().BeTrue();
        }

        [Theory]
        [InlineData("LANG=en_US.UTF-8", true)]
        [InlineData("LC_ALL=C;LANG=en_US.UTF-8", false)]
        [InlineData("LC_CTYPE=UTF-8", true)]
        [InlineData("LANG=C.utf8", true)]
        [InlineData("", false)]
        [InlineData("LANG=C", false)]
        [InlineData("LC_ALL=;LANG=en_US.UTF-8", true)]
        public void IsUtf8Locale_PrecedenceAndMatching(string variables, bool expected)
        {
            TerminalEnvironment.IsUtf8Locale(isWindows: false, Environment(variables)).Should().Be(expected);
        }

        [Fact]
        public void Utf8Warning_MentionsLangAndTmuxOnlyInsideTmux()
        {
            const string expected = "Warning: the terminal locale is not UTF-8, so the admin console may not draw its borders correctly. Set LANG=C.UTF-8 before starting MBBSEmu.";

            TerminalEnvironment.Utf8Warning(insideTmux: false).Should().Be(expected);
            TerminalEnvironment.Utf8Warning(insideTmux: true).Should().Be(expected + " Inside tmux, also start tmux with -u.");
        }
    }
}
