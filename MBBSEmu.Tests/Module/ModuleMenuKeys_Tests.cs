using FluentAssertions;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    public class ModuleMenuKeys_Tests
    {
        private static readonly IEnumerable<string> LettersExceptX =
            Enumerable.Range('A', 26).Select(x => ((char)x).ToString()).Where(x => x != "X");

        private static readonly IEnumerable<string> SingleDigits = Enumerable.Range(1, 9).Select(x => x.ToString());

        private static readonly IEnumerable<string> TwoDigits = Enumerable.Range(10, 90).Select(x => x.ToString());

        [Fact]
        public void Next_NothingUsed_ReturnsA()
        {
            ModuleMenuKeys.Next(new string[0]).Should().Be("A");
        }

        [Fact]
        public void Next_SkipsX()
        {
            ModuleMenuKeys.Next(Enumerable.Range('A', 23).Select(x => ((char)x).ToString())).Should().Be("Y");
        }

        [Fact]
        public void Next_LettersUsed_ReturnsDigits()
        {
            ModuleMenuKeys.Next(LettersExceptX).Should().Be("1");
        }

        [Fact]
        public void Next_ComparesCaseInsensitively()
        {
            ModuleMenuKeys.Next(new[] { "a" }).Should().Be("B");
        }

        [Fact]
        public void Next_SingleDigitsUsed_ReturnsTwoDigits()
        {
            ModuleMenuKeys.Next(LettersExceptX.Concat(SingleDigits)).Should().Be("10");
        }

        [Fact]
        public void Next_EverythingUsed_ReturnsNull()
        {
            ModuleMenuKeys.Next(LettersExceptX.Concat(SingleDigits).Concat(TwoDigits)).Should().BeNull();
        }

        [Fact]
        public void Next_IgnoresNullAndEmptyKeys()
        {
            ModuleMenuKeys.Next(new[] { null, "", "A" }).Should().Be("B");
        }
    }
}
