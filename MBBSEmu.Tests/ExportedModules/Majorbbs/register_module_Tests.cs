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
                sttrou = new FarPtr(0x0001, 0x0001),
                dlarou = new FarPtr(0x0002, 0x0002),
                finrou = new FarPtr(0x0003, 0x0003),
                huprou = new FarPtr(0x0004, 0x0004),
                injrou = new FarPtr(0x0005, 0x0005),
                lofrou = new FarPtr(0x0006, 0x0006),
                lonrou = new FarPtr(0x0007, 0x0007),
                mcurou = new FarPtr(0x0008, 0x0008),
                stsrou = new FarPtr(0x0009, 0x0009)
            };

            var modulePointer = mbbsEmuMemoryCore.AllocateVariable("MODULE", ModuleStruct.Size);
            mbbsEmuMemoryCore.SetArray(modulePointer, moduleStruct.Data);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, REGISTER_MODULE_ORDINAL, new List<FarPtr> { modulePointer });

            Assert.Equal(Encoding.ASCII.GetString(moduleStruct.descrp).TrimEnd('\0'), mbbsModule.ModuleDescription);
            Assert.Equal(new FarPtr(0x0001, 0x0001), mbbsModule.MainModuleDll.EntryPoints["sttrou"]);
            Assert.Equal(new FarPtr(0x0002, 0x0002), mbbsModule.MainModuleDll.EntryPoints["dlarou"]);
            Assert.Equal(new FarPtr(0x0003, 0x0003), mbbsModule.MainModuleDll.EntryPoints["finrou"]);
            Assert.Equal(new FarPtr(0x0004, 0x0004), mbbsModule.MainModuleDll.EntryPoints["huprou"]);
            Assert.Equal(new FarPtr(0x0005, 0x0005), mbbsModule.MainModuleDll.EntryPoints["injrou"]);
            Assert.Equal(new FarPtr(0x0006, 0x0006), mbbsModule.MainModuleDll.EntryPoints["lofrou"]);
            Assert.Equal(new FarPtr(0x0007, 0x0007), mbbsModule.MainModuleDll.EntryPoints["lonrou"]);
            Assert.Equal(new FarPtr(0x0008, 0x0008), mbbsModule.MainModuleDll.EntryPoints["mcurou"]);
            Assert.Equal(new FarPtr(0x0009, 0x0009), mbbsModule.MainModuleDll.EntryPoints["stsrou"]);
            Assert.NotEqual(FarPtr.Empty, mbbsEmuMemoryCore.GetPointer(mbbsEmuMemoryCore.GetVariablePointer("MODULE") + (2 * mbbsModule.ModuleDlls[0].StateCode)));

            //Verify the Local Copy
            var localModulePointer =
                mbbsEmuMemoryCore.GetPointer(
                    mbbsEmuMemoryCore.GetVariablePointer("MODULE") + (2 * mbbsModule.ModuleDlls[0].StateCode));

            var localModuleStruct = new ModuleStruct(mbbsEmuMemoryCore.GetArray(localModulePointer, ModuleStruct.Size));

            Assert.Equal(Encoding.ASCII.GetString(moduleStruct.descrp).TrimEnd('\0'), Encoding.ASCII.GetString(localModuleStruct.descrp).TrimEnd('\0'));
            Assert.Equal(new FarPtr(0x0001, 0x0001), localModuleStruct.sttrou);
            Assert.Equal(new FarPtr(0x0002, 0x0002), localModuleStruct.dlarou);
            Assert.Equal(new FarPtr(0x0003, 0x0003), localModuleStruct.finrou);
            Assert.Equal(new FarPtr(0x0004, 0x0004), localModuleStruct.huprou);
            Assert.Equal(new FarPtr(0x0005, 0x0005), localModuleStruct.injrou);
            Assert.Equal(new FarPtr(0x0006, 0x0006), localModuleStruct.lofrou);
            Assert.Equal(new FarPtr(0x0007, 0x0007), localModuleStruct.lonrou);
            Assert.Equal(new FarPtr(0x0008, 0x0008), localModuleStruct.mcurou);
            Assert.Equal(new FarPtr(0x0009, 0x0009), localModuleStruct.stsrou);
        }
    }
}
