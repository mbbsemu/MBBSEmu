using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class outprf_Tests : ExportedModuleTestBase
    {
        private const int OUTPRF_ORDINAL = 463;

        [Fact]
        public void outprf_InvalidChannel_Test()
        {
            Reset();

            SetInput("Test");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFF });

            //Shouldn't error
        }

        /// <summary>
        ///     Regression test for https://github.com/mbbsemu/MBBSEmu/issues/647
        ///
        ///     outprf() must NOT clear prfbuf -- only clrprf() should. Modules (e.g. Galactic
        ///     Empire's hailing messages) rely on calling prf() once and then outprf() repeatedly
        ///     to broadcast the same buffered text to multiple channels.
        /// </summary>
        [Fact]
        public void outprf_DoesNotClearBuffer_Test()
        {
            Reset();

            const string inputValue = "Test";
            SetInput(inputValue);

            var prfBufPointerBefore = mbbsEmuMemoryCore.GetVariablePointer("PRFBUF");
            var prfPtrBefore = mbbsEmuMemoryCore.GetPointer("PRFPTR");

            //Call outprf() multiple times, simulating broadcasting the same buffer to multiple channels
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFF });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFE });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFD });

            //PRFPTR should be untouched by outprf()
            var prfPtrAfter = mbbsEmuMemoryCore.GetPointer("PRFPTR");
            Assert.Equal(prfPtrBefore, prfPtrAfter);

            //PRFBUF contents should still be intact (not zeroed out)
            var prfBufAfter = mbbsEmuMemoryCore.GetString(prfBufPointerBefore, stripNull: true);
            Assert.Equal(inputValue, Encoding.ASCII.GetString(prfBufAfter));
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
