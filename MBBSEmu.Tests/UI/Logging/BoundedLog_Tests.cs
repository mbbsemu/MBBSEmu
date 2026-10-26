using FluentAssertions;
using MBBSEmu.UI.Logging;
using System;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.UI.Logging
{
    public class BoundedLog_Tests
    {
        private static int[] Items(int from, int count) => Enumerable.Range(from, count).ToArray();

        [Fact]
        public void Append_UnderCapacity_DropsNothing()
        {
            var log = new BoundedLog<int>(5);

            log.Append(Items(1, 3)).Should().Be(0);

            log.Count.Should().Be(3);
            log[0].Should().Be(1);
            log[2].Should().Be(3);
        }

        [Fact]
        public void Append_OverCapacity_DropsOldest()
        {
            var log = new BoundedLog<int>(5);
            log.Append(Items(1, 3));

            log.Append(Items(4, 4)).Should().Be(2);

            log.Count.Should().Be(5);
            log[0].Should().Be(3);
            log[4].Should().Be(7);
        }

        [Fact]
        public void Append_BatchLargerThanCapacity_KeepsNewest()
        {
            var log = new BoundedLog<int>(5);

            log.Append(Items(1, 12)).Should().Be(7);

            log.Count.Should().Be(5);
            log[0].Should().Be(8);
            log[4].Should().Be(12);
        }

        [Fact]
        public void Append_RepeatedWraps_KeepsOrder()
        {
            var log = new BoundedLog<int>(3);
            for (var i = 1; i <= 10; i++)
                log.Append(new[] { i });

            new[] { log[0], log[1], log[2] }.Should().Equal(8, 9, 10);
        }

        [Fact]
        public void Indexer_OutOfRange_Throws()
        {
            var log = new BoundedLog<int>(5);
            log.Append(Items(1, 2));

            var act = () => log[2];

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
