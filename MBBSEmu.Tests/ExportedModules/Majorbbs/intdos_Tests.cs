using MBBSEmu.CPU;
using MBBSEmu.DOS;
using MBBSEmu.Extensions;
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
        public void intdos_0x19_GetDefaultDisk_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x19};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(2, testRegisters.AL);
        }

        [Fact]
        public void intdos_0x1A_FCBMemory_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters { AH = 0x1A, DS = 0xFF, DX = 0xFF };

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0xFF, testRegisters.DS);
            Assert.Equal(0xFF, testRegisters.DX);
        }

        [Fact]
        public void intdos_0x25_SetInterruptVector_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x25, AL = 0x8};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0, testRegisters.DS);
            Assert.Equal(0, testRegisters.DX);
        }

        [Theory]
        [InlineData(1979, 1, 1)]
        [InlineData(1981, 2, 2)]
        [InlineData(2020, 3, 3)]
        [InlineData(1985, 5, 4)]
        [InlineData(1987, 6, 5)]
        [InlineData(1995, 8, 6)]
        [InlineData(1999, 10, 7)]
        [InlineData(2000, 12, 8)]
        public void intdos_0x2A_GetCurrentDate_Test(int year, int month, int day)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x2A};

            fakeClock.Now = new DateTime(year, month, day);

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(fakeClock.Now.Day, testRegisters.DL);
            Assert.Equal(fakeClock.Now.Month, testRegisters.DH);
            Assert.Equal(fakeClock.Now.Year, testRegisters.CX);
            Assert.Equal((byte) fakeClock.Now.DayOfWeek, testRegisters.AL);
        }

        [Theory]
        [InlineData(2, 1, 1, 0)]
        [InlineData(4, 2, 2, 100)]
        [InlineData(6, 3, 3, 200)]
        [InlineData(10, 5, 10, 400)]
        [InlineData(12, 6, 25, 620)]
        [InlineData(14, 8, 45, 750)]
        [InlineData(22, 10, 50, 819)]
        [InlineData(23, 59, 59, 900)]
        public void intdos_0x2C_GetCurrentTime_Test(int hour, int minute, int seconds, int milliseconds)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x2C};

            fakeClock.Now = new DateTime(2020, 1, 6, hour, minute, seconds, milliseconds);

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(fakeClock.Now.Hour, testRegisters.CH);
            Assert.Equal(fakeClock.Now.Minute, testRegisters.CL);
            Assert.Equal(fakeClock.Now.Second, testRegisters.DH);
            Assert.Equal((fakeClock.Now.Millisecond / 10), testRegisters.DL);
        }

        [Fact]
        public void intdos_0x2F_GetDTAAddress_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x2F};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort) testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> {testRegistersPointer, testRegistersPointer});

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort) testRegistersArrayData.Length));

            //Verify Results
            //Assert.Equal(0xF000, testRegisters.ES); // regs doesn't return ES
            Assert.Equal(0x1000, testRegisters.BX);
        }

        [Fact]
        public void intdos_0x30_GetDOSVersion_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x30};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(6, testRegisters.AL);
            Assert.Equal(22, testRegisters.AH);
        }

        [Theory]
        [InlineData(0x8, 0x0, 0x8)]
        [InlineData(0x0, 0x0, 0x0)]
        public void intdos_0x35_GetInterruptVector_Test(ushort registerALvalue, ushort registerESvalue, ushort registerBXvalue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters { AH = 0x35, AL = (byte)registerALvalue };

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results -- TODO not sure this is right
            Assert.Equal(registerESvalue, testRegisters.ES);
            Assert.Equal(registerBXvalue, testRegisters.BX);
        }

        [Fact]
        public void intdos_0x40_WriteToFile_Test()
        {
            //TODO
        }

        [Fact]
        public void intdos_0x44_GetDeviceInformation_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x44, AL = 0};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0, testRegisters.BX);
        }

        [Fact]
        public void intdos_0x47_GetCurrentDirectory_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x47};

            var dirPointer = mbbsEmuMemoryCore.AllocateVariable("DIR-POINTER", 10);
            mbbsEmuCpuRegisters.DS = dirPointer.Segment;
            testRegisters.SI = dirPointer.Offset;

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.Equal(2, testRegisters.DL); // C Drive
            Assert.Equal(dirPointer.Segment, mbbsEmuCpuRegisters.DS);
            Assert.Equal(dirPointer.Offset, testRegisters.SI);
            Assert.Equal("BBSV6", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(testRegisters.DS, testRegisters.SI, true)));
        }

        [Fact]
        public void intdos_0x4A_AdjustMemoryBlockSize_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x4A};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(0xFFFF, testRegisters.BX);
            Assert.False(testRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Fact]
        public void intdos_0x4C_QuitWithExitCode_Test()
        {
            // TODO
        }

        [Fact]
        public void intdos_0x62_GetPSPAddress_Test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var testRegisters = new CpuRegisters {AH = 0x62};

            //Allocate some memory to hold the test data
            var testRegistersArrayData = testRegisters.ToRegs();
            var testRegistersPointer = mbbsEmuMemoryCore.AllocateVariable(null, (ushort)testRegistersArrayData.Length);
            mbbsEmuMemoryCore.SetArray(testRegistersPointer, testRegistersArrayData);

            //Allocate PSP variable
            var pspPointer = mbbsEmuMemoryCore.AllocateVariable("Int21h-PSP", 0xFF);
            mbbsEmuMemoryCore.SetArray("Int21h-PSP", Encoding.ASCII.GetBytes("8"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, INTDOS_ORDINAL,
                new List<FarPtr> { testRegistersPointer, testRegistersPointer });

            //Data returned in memory, so we need to reload testRegisters from memory
            testRegisters.FromRegs(mbbsEmuMemoryCore.GetArray(testRegistersPointer,
                (ushort)testRegistersArrayData.Length));

            //Verify Results
            Assert.Equal(56, testRegisters.BX);
        }
    }
}
