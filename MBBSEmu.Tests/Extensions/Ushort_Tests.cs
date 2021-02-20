using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.Extensions
{
    public class Ushort_Tests
    {
        [Theory]
        [InlineData(2020, 01, 01, 20513)]
        [InlineData(1980, 01, 01, 33)]
        [InlineData(2022, 06, 04, 21700)]
        [InlineData(2010, 12, 31, 15775)]
        [InlineData(2000, 04, 03, 10371)]
        public void ToDosDate_Test(ushort srcYr, ushort srcMo, ushort srcDay, ushort expectedDosDate)
        {
            var sourceDate = new DateTime(srcYr, srcMo, srcDay);
            
            //Verify Results
            Assert.Equal(expectedDosDate, sourceDate.ToDosDate());
        }

        [Theory]
        [InlineData(20513, 2020, 01, 01)]
        [InlineData(33, 1980, 01, 01)]
        [InlineData(21700, 2022, 06, 04)]
        [InlineData(15775, 2010, 12, 31)]
        [InlineData(10371, 2000, 04, 03)]
        public void FromDosDate_Test(ushort srcDosDate, ushort expYr, ushort expMo, ushort expDay)
        {
            var expectedDate = new DateTime(expYr, expMo, expDay);

            //Verify Results
            Assert.Equal(expectedDate, srcDosDate.FromDosDate());
        }
    }
}
