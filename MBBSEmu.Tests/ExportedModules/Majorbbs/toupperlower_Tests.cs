using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class toupperlower_Tests : ExportedModuleTestBase
    {
        private const int TOLOWER_ORDINAL = 603;
        private const int TOUPPER_ORDINAL = 604;
        
        [Theory]
        [InlineData('A', 'a')]
        [InlineData('B', 'b')]
        [InlineData('Z', 'z')]
        [InlineData('j', 'j')]
        [InlineData('%', '%')]
        [InlineData('*', '*')]
        [InlineData('1', '1')]

        public void ToLowerTest(char inputChar, ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TOLOWER_ORDINAL, new List<ushort> { inputChar });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
        }

        [Theory]
        [InlineData('a', 'A')]
        [InlineData('b', 'B')]
        [InlineData('z', 'Z')]
        [InlineData('J', 'J')]
        [InlineData('%', '%')]
        [InlineData('*', '*')]
        [InlineData('1', '1')]
        public void ToUpperTest(char inputChar, ushort expectedValue)
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TOUPPER_ORDINAL, new List<ushort> { inputChar });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
        }
    }
}
