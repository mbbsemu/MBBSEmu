using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class register_module_Tests : ExportedModuleTestBase
    {
        private const int REGISTER_MODULE_ORDINAL = 492;

        [Fact]
        public void register_module_test()
        {
            var moduleStruct = new ModuleStruct
            {
                descrp = Encoding.ASCII.GetBytes("Test Module\0"),
                sttrou = new IntPtr16(0x0001, 0x0001),
                dlarou = new IntPtr16(0x0002, 0x0002),
                finrou = new IntPtr16(0x0003, 0x0003),
                huprou = new IntPtr16(0x0004, 0x0004),
                injrou = new IntPtr16(0x0005, 0x0005),
                lofrou = new IntPtr16(0x0006, 0x0006),
                lonrou = new IntPtr16(0x0007, 0x0007),
                mcurou = new IntPtr16(0x0008, 0x0008),
                stsrou = new IntPtr16(0x0009, 0x0009)
            };

            var modulePointer = mbbsEmuMemoryCore.AllocateVariable("MODULE", ModuleStruct.Size);
            mbbsEmuMemoryCore.SetArray(modulePointer, moduleStruct.Data);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, REGISTER_MODULE_ORDINAL, new List<IntPtr16> { modulePointer });

            Assert.Equal(Encoding.ASCII.GetString(moduleStruct.descrp).TrimEnd('\0'), mbbsModule.ModuleDescription);
            Assert.Equal(new IntPtr16(0x0001, 0x0001), mbbsModule.EntryPoints["sttrou"]);
            Assert.Equal(new IntPtr16(0x0002, 0x0002), mbbsModule.EntryPoints["dlarou"]);
            Assert.Equal(new IntPtr16(0x0003, 0x0003), mbbsModule.EntryPoints["finrou"]);
            Assert.Equal(new IntPtr16(0x0004, 0x0004), mbbsModule.EntryPoints["huprou"]);
            Assert.Equal(new IntPtr16(0x0005, 0x0005), mbbsModule.EntryPoints["injrou"]);
            Assert.Equal(new IntPtr16(0x0006, 0x0006), mbbsModule.EntryPoints["lofrou"]);
            Assert.Equal(new IntPtr16(0x0007, 0x0007), mbbsModule.EntryPoints["lonrou"]);
            Assert.Equal(new IntPtr16(0x0008, 0x0008), mbbsModule.EntryPoints["mcurou"]);
            Assert.Equal(new IntPtr16(0x0009, 0x0009), mbbsModule.EntryPoints["stsrou"]);
            Assert.NotEqual(IntPtr16.Empty, mbbsEmuMemoryCore.GetPointer(mbbsEmuMemoryCore.GetVariablePointer("MODULE") + (2 * mbbsModule.StateCode)));
        }
    }
}
