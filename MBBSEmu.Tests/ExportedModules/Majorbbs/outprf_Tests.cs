using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class outprf_Tests : ExportedModuleTestBase
    {
        private const int OUTPRF_ORDINAL = 463;

        [Fact]
        public void outprf_rtkick_Test()
        {
            Reset();

            SetInput("Test");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> {0xFFFF});

            //Shouldn't error
        }

        private void SetInput(string inputValue)
        {
            //Set Input Value
            mbbsEmuMemoryCore.SetArray("PRFBUF", Encoding.ASCII.GetBytes(inputValue));

            //Set PRFPTR
            var prfPointer = mbbsEmuMemoryCore.GetPointer("PRFPTR");
            prfPointer.Offset += (ushort)inputValue.Length;
            mbbsEmuMemoryCore.SetPointer("PRFPTR", prfPointer);
        }
    }
}
