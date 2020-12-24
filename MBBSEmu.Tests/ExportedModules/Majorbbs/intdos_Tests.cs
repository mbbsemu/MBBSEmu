using MBBSEmu.Memory;
using MBBSEmu.CPU;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class intdos_Tests : ExportedModuleTestBase
    {
        private const int INTDOS_ORDINAL = 355;

        [Fact]
        public void intdos_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            var testRegistersPointer = testRegisters.GetPointer();

            testRegisters.AH = 0x2A;

            mbbsEmuCpuRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer, 16));


            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL, new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Verify Results
            Assert.Equal((byte) DateTime.Now.Month, mbbsEmuCpuRegisters.DL);
        }
    }
}
