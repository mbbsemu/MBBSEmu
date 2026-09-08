using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class outprf_Tests : ExportedModuleTestBase
    {
        private const int OUTPRF_ORDINAL = 463;
        private const int PRF_ORDINAL = 474;
        private const int PRFMSG_ORDINAL = 476;
        private const int PRFMLT_ORDINAL = 178;

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

            //PRFPTR and PRFBUF remain intact for repeated sends
            var prfPtrAfter = mbbsEmuMemoryCore.GetPointer("PRFPTR");
            Assert.Equal(prfPtrBefore, prfPtrAfter);

            //PRFBUF contents should still be intact (not zeroed out)
            var prfBufAfter = mbbsEmuMemoryCore.GetString(prfBufPointerBefore, stripNull: true);
            Assert.Equal(inputValue, Encoding.ASCII.GetString(prfBufAfter));
        }

        [Fact]
        public void prf_AfterOutprfStartsNewBuffer_Test()
        {
            Reset();
            SetInput("Previous message");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL,
                new List<ushort> { 0xFFFF });

            AppendWithPrf("First row");
            AppendWithPrf("Second row");

            Assert.Equal("First rowSecond row",
                Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
        }

        [Fact]
        public void prf_AfterOutprfAndChannelIndependentSetStateStartsNewBuffer_Test()
        {
            Reset();
            SetInput("Previous callback output");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL,
                new List<ushort> { 0xFFFF });

            majorbbs.SetState(ushort.MaxValue);
            AppendWithPrf("New callback output");

            Assert.Equal("New callback output",
                Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
        }

        [Theory]
        [InlineData(PRFMSG_ORDINAL)]
        [InlineData(PRFMLT_ORDINAL)]
        public void MessageFormatter_AfterOutprfStartsNewBuffer_Test(int formatterOrdinal)
        {
            Reset();
            SetInput("Previous message");
            SetMcvMessage("New message");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL,
                new List<ushort> { 0xFFFF });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)formatterOrdinal,
                new List<ushort> { 0 });

            Assert.Equal("New message",
                Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
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

        private void AppendWithPrf(string inputValue)
        {
            var inputPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(),
                (ushort)(inputValue.Length + 1), true);
            mbbsEmuMemoryCore.SetArray(inputPointer, Encoding.ASCII.GetBytes(inputValue));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PRF_ORDINAL,
                new List<ushort> { inputPointer.Offset, inputPointer.Segment });
        }

        private void SetMcvMessage(string message)
        {
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(message) } }));
            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));
        }
    }
}
