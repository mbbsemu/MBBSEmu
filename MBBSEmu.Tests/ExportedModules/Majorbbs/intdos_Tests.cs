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
            testRegisters.AH = 0x2A;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL, new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer, (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(DateTime.Now.Day, testRegisters.DL);
            Assert.Equal(DateTime.Now.Month, testRegisters.DH);
            Assert.Equal(DateTime.Now.Year, testRegisters.CX);
            Assert.Equal((byte)DateTime.Now.DayOfWeek, testRegisters.AL);
        }
    }
}
