using FluentAssertions;
using MBBSEmu.UI.Main;
using System.Collections.Generic;
using Terminal.Gui.Input;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class AdminConsoleKeys_Tests
    {
        public static IEnumerable<object[]> Keys => new[]
        {
            new object[] { Key.Q.WithCtrl, EnumConsoleKeyAction.Shutdown },
            new object[] { Key.C.WithCtrl, EnumConsoleKeyAction.Shutdown },
            new object[] { Key.O.WithCtrl, EnumConsoleKeyAction.LoadModule },
            new object[] { Key.Esc, EnumConsoleKeyAction.Swallow },
            new object[] { Key.Z.WithCtrl, EnumConsoleKeyAction.Swallow },
            new object[] { Key.Tab, EnumConsoleKeyAction.None },
            new object[] { Key.Enter, EnumConsoleKeyAction.None },
            new object[] { Key.A, EnumConsoleKeyAction.None },
            new object[] { Key.Q, EnumConsoleKeyAction.None }
        };

        [Theory]
        [MemberData(nameof(Keys))]
        public void Classify(Key key, EnumConsoleKeyAction expected)
        {
            AdminConsoleKeys.Classify(key).Should().Be(expected);
        }

        public static IEnumerable<object[]> KeysUnderDialog => new[]
        {
            new object[] { Key.Z.WithCtrl, EnumConsoleKeyAction.Swallow },
            new object[] { Key.Esc, EnumConsoleKeyAction.None },
            new object[] { Key.Q.WithCtrl, EnumConsoleKeyAction.None },
            new object[] { Key.C.WithCtrl, EnumConsoleKeyAction.None },
            new object[] { Key.O.WithCtrl, EnumConsoleKeyAction.None },
            new object[] { Key.Enter, EnumConsoleKeyAction.None }
        };

        /// <summary>
        ///     With a dialog on top, the dialog handles its own keys (Esc closes it), but Ctrl+Z must still never
        ///     reach Terminal.Gui's Suspend binding: a suspended MBBSEmu freezes every caller
        /// </summary>
        [Theory]
        [MemberData(nameof(KeysUnderDialog))]
        public void Decide_DialogOnTop(Key key, EnumConsoleKeyAction expected)
        {
            AdminConsoleKeys.Decide(key, consoleOnTop: false).Should().Be(expected);
        }

        [Theory]
        [MemberData(nameof(Keys))]
        public void Decide_ConsoleOnTop_MatchesClassify(Key key, EnumConsoleKeyAction expected)
        {
            AdminConsoleKeys.Decide(key, consoleOnTop: true).Should().Be(expected);
        }
    }
}
