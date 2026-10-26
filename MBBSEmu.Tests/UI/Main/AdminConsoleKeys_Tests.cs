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
    }
}
