using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Module;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class key_Tests : ExportedModuleTestBase
    {

        private const ushort HASKEY_ORDINAL = 334;
        private const ushort HASMKEY_ORDINAL = 335;
        private const ushort UIDKEY_ORDINAL = 609;

        [Fact]
        public void haskey_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "sysop";
            var key = "SYSOP";

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(key.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes("SYSOP"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HASKEY_ORDINAL, new List<IntPtr16> { stringPointer });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }

        [Fact]
        public void hasmkey_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "sysop";
            var key = "NORMAL";

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(key) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new IntPtr16(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HASMKEY_ORDINAL, new List<ushort> { 0 });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }

        [Fact]
        public void hasmkey_Empty_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "sysop";

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, new byte[] { 0 } } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new IntPtr16(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HASMKEY_ORDINAL, new List<ushort> { 0 });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }

        [Fact]
        public void haskey_Empty_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "sysop";
            var key = "";

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(key.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes("SYSOP"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HASKEY_ORDINAL, new List<IntPtr16> { stringPointer });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }

        [Fact]
        public void uidkey_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            var username = "guest";
            var key = "NORMAL";

            //Set Argument Values to be Passed In
            var usernamePointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(username.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(username));
            var keyPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING2", (ushort)(key.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING2", Encoding.ASCII.GetBytes(key));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, UIDKEY_ORDINAL, new List<IntPtr16> { usernamePointer, keyPointer });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }

        protected override void Reset()
        {
            base.Reset();

            _serviceResolver.GetService<IAccountRepository>().Reset("sysop");
            _serviceResolver.GetService<IAccountKeyRepository>().Reset();
        }
    }
}
