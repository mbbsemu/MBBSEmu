using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
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
        ///
        ///     Like GCOMM's outprf(), it does move prfptr back to the start of prfbuf.
        /// </summary>
        [Fact]
        public void outprf_DoesNotClearBuffer_Test()
        {
            Reset();

            const string inputValue = "Test";
            SetInput(inputValue);

            var prfBufPointerBefore = mbbsEmuMemoryCore.GetVariablePointer("PRFBUF");

            //Call outprf() multiple times, simulating broadcasting the same buffer to multiple channels
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFF });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFE });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0xFFFD });

            //PRFPTR is back at the start of PRFBUF
            Assert.Equal(prfBufPointerBefore, mbbsEmuMemoryCore.GetPointer("PRFPTR"));

            //PRFBUF contents should still be intact (not zeroed out)
            var prfBufAfter = mbbsEmuMemoryCore.GetString(prfBufPointerBefore, stripNull: true);
            Assert.Equal(inputValue, Encoding.ASCII.GetString(prfBufAfter));
        }

        /// <summary>
        ///     Galactic Empire's outsect() formats a message once, calls outprf() for every ship in
        ///     the sector, then calls clrprf(). Every channel must receive the message.
        /// </summary>
        [Fact]
        public void outprf_RepeatedSendsReachEveryChannel_Test()
        {
            ResetForChannelOutput();

            AppendWithPrf("Hail");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0 });
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 1 });

            Assert.Equal("Hail", Encoding.ASCII.GetString(((TestSession)testSessions[0]).DrainSentData()));
            Assert.Equal("Hail", Encoding.ASCII.GetString(((TestSession)testSessions[1]).DrainSentData()));
        }

        /// <summary>
        ///     Trade Wars 2002 flushes with "if (prfptr > prfbuf) outprf(usrnum);" from more than one
        ///     place in a single step. After outprf() there must be nothing left pending, or the same
        ///     output is sent twice.
        /// </summary>
        [Fact]
        public void outprf_NothingPendingAfterSend_Test()
        {
            ResetForChannelOutput();

            AppendWithPrf("Command [TL=00:00:00]:");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0 });

            Assert.Equal(mbbsEmuMemoryCore.GetVariablePointer("PRFBUF"), mbbsEmuMemoryCore.GetPointer("PRFPTR"));
            Assert.Equal("Command [TL=00:00:00]:", Encoding.ASCII.GetString(((TestSession)testSessions[0]).DrainSentData()));
        }

        /// <summary>
        ///     prf(); outprf(); prf(); outprf(); sends each message once.
        /// </summary>
        [Fact]
        public void outprf_AlternatingWithPrfSendsEachMessageOnce_Test()
        {
            ResetForChannelOutput();

            AppendWithPrf("Long first message");
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0 });
            AppendWithPrf("Second");
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, OUTPRF_ORDINAL, new List<ushort> { 0 });

            Assert.Equal("Long first messageSecond", Encoding.ASCII.GetString(((TestSession)testSessions[0]).DrainSentData()));
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

        /// <summary>
        ///     Starts a channel 0 callback, so PRFPTR points at PRFBUF. The test sessions have no
        ///     host, so the host-backed text variables can't be evaluated when output is sent to them.
        /// </summary>
        private void ResetForChannelOutput()
        {
            Reset();
            majorbbs.SetState(0);
            testSessions[0].SessionVariables.Clear();
            testSessions[1].SessionVariables.Clear();
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
