using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class dcdate_Tests : ExportedModuleTestBase
    {
        private const int DCDATE_ORDINAL = 157;

        [Theory]
        [InlineData("09/17/20", 20785)]
        [InlineData("12/31/90", 5535)]
        [InlineData("1/1/80", 33)]
        [InlineData("13/32/00", ushort.MaxValue)] //Invalid Date String
        [InlineData("test", ushort.MaxValue)] //Invalid Date String
        public void DCDATE_Test(string inputString, ushort expectedValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DCDATE_ORDINAL, new List<FarPtr> { string1Pointer });

            //Verify Results

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);

        }

        [Theory]
        [InlineData("en-US")] //Month-first
        [InlineData("en-GB")] //Day-first
        [InlineData("en-IN")] //Day-first
        [InlineData("de-DE")] //Day-first, dotted separators
        public void DCDATE_IsIndependentOfHostCulture(string cultureName)
        {
            //Module date strings are always MM/DD/YY. A day-first host culture would read
            //"12/31/90" as day 12 of month 31 and reject it, so the same input must decode
            //identically no matter what the host is set to.
            const string inputString = "12/31/90";
            const ushort expectedValue = 5535;

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

                Reset();

                var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(inputString.Length + 1));
                mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(inputString));

                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DCDATE_ORDINAL, new List<FarPtr> { string1Pointer });

                Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }
    }
}
