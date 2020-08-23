using MBBSEmu.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Extensions
{
    public class EnumerableExtensions_Tests
    {
        [Fact]
        public void SearchWithZeroLengthReturnsNothing()
        {
            int[] t = {1, 2, 3, 4};

            int[] indices = t.FindIndexes(0, i => i == 1).ToArray();
            Assert.Equal(new int[0], indices);
        }

        [Fact]
        public void SearchWithSingleLengthReturnsAnswer()
        {
            int[] t = {1, 2, 3, 4};

            int[] indices = t.FindIndexes(1, i => i == 1).ToArray();
            Assert.Equal(new int[] {0}, indices);
        }

        [Fact]
        public void SearchWithFullLengthReturnsAnswer()
        {
            int[] t = {1, 2, 3, 4};

            int[] indices = t.FindIndexes(t.Length, i => i == 2).ToArray();
            Assert.Equal(new int[] {1}, indices);
        }

        [Fact]
        public void SearchWithShortenedLengthReturnsNothing()
        {
            int[] t = {1, 2, 3, 4};

            int[] indices = t.FindIndexes(3, i => i == 4).ToArray();
            Assert.Equal(new int[0], indices);
        }

        [Fact]
        public void SearchReturnsMany()
        {
            int[] t = {1, 2, 3, 4};

            int[] indices = t.FindIndexes(4, i => i % 2 == 0).ToArray();
            Assert.Equal(new int[2] {1, 3}, indices);
        }
    }
}
