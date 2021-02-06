using System.Collections.Generic;
using System.Text;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Sequential")]
    public class hdluid_Tests : ExportedModuleTestBase
    {
        private const ushort HDLUID_ORDINAL = 338;

        [Theory]
        [InlineData("gu", 0)]
        [InlineData("Gue", 0)]
        [InlineData("G", 0)]
        [InlineData("Sy", 0)]
        [InlineData("syso", 0)]
        [InlineData("Merl", 0xFFFF)]
        [InlineData("", 0xFFFF)]
        public void hdluid_Test(string usernamePartial, ushort expectedResult)
        {
            Reset();

            _serviceResolver.GetService<IAccountRepository>();

            //Set Argument Values to be Passed In
            var usernamePointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(usernamePartial.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(usernamePartial));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HDLUID_ORDINAL, new List<FarPtr> { usernamePointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
        }

        protected override void Reset()
        {
            base.Reset();

            _serviceResolver.GetService<IAccountRepository>().Reset("sysop");
        }
    }
}
