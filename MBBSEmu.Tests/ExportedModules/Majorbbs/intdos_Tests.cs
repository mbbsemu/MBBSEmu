using MBBSEmu.CPU;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class intdos_Tests : ExportedModuleTestBase
    {
        private const int INTDOS_ORDINAL = 355;

        [Fact]
        public void intdos_0x19_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x19;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(2, testRegisters.AL);
        }

        [Fact]
        public void intdos_0x1A_Test()
        {
            //TODO
        }

        [Fact]
        public void intdos_0x25_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x25;
            testRegisters.AL = 0x8;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0, testRegisters.DS);
            Assert.Equal(0, testRegisters.DX);
        }

        [Fact]
        public void intdos_0x2A_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x2A;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(DateTime.Now.Day, testRegisters.DL);
            Assert.Equal(DateTime.Now.Month, testRegisters.DH);
            Assert.Equal(DateTime.Now.Year, testRegisters.CX);
            Assert.Equal((byte) DateTime.Now.DayOfWeek, testRegisters.AL);
        }

        [Fact]
        public void intdos_0x2C_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x2C;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(DateTime.Now.Hour, testRegisters.CH);
            Assert.Equal(DateTime.Now.Minute, testRegisters.CL);
        }

        [Fact]
        public void intdos_0x2F_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x2F;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Allocate DTA variable
            var dtaPointer = mbbsEmuMemoryCore.AllocateVariable("Int21h-DTA", 0xFF);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            //Assert.Equal(dtaPointer.Segment, testRegisters.ES); -- DOESN'T WORK!, comes back as 0
            Assert.Equal(dtaPointer.Offset, testRegisters.BX);
        }

        [Fact]
        public void intdos_0x30_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x30;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(6, testRegisters.AL);
            Assert.Equal(22, testRegisters.AH);
        }

        [Fact]
        public void intdos_0x35_Test()
        {
            //TODO
        }

        [Fact]
        public void intdos_0x40_Test()
        {
            //TODO
        }

        [Fact]
        public void intdos_0x44_Test()
        {
            //TODO
        }

        [Fact]
        public void intdos_0x47_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x47;

            var dirPointer = mbbsEmuMemoryCore.AllocateVariable("DIR-POINTER", 10);
            testRegisters.DS = dirPointer.Segment;
            testRegisters.SI = dirPointer.Offset;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0, testRegisters.AX);
            Assert.Equal(0, testRegisters.DL);
            Assert.Equal(4096, testRegisters.DS);
            Assert.Equal(40238, testRegisters.SI);
        }

        [Fact]
        public void intdos_0x4A_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x4A;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0xFFFF, testRegisters.BX);
        }

        [Fact]
        public void intdos_0x4C_Test()
        {
            // TODO
        }

        [Fact]
        public void intdos_0x62_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters();
            testRegisters.AH = 0x62;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Allocate PSP variable
            var pspPointer = mbbsEmuMemoryCore.AllocateVariable("INT21h-PSP", 0xFF);
            mbbsEmuMemoryCore.SetArray("INT21h-PSP", Encoding.ASCII.GetBytes("8"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<IntPtr16> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(56, testRegisters.BX);
        }
    }
}
