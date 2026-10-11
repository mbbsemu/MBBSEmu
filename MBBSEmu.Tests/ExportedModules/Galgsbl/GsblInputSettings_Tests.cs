using MBBSEmu.Memory;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    /// <summary>
    ///     GALGSBL routines that control or read channel input
    /// </summary>
    public class GsblInputSettings_Tests : ExportedModuleTestBase
    {
        private const ushort BTUBSE_ORDINAL = 1;
        private const ushort BTUCLI_ORDINAL = 6;
        private const ushort BTUECH_ORDINAL = 11;
        private const ushort BTUIBW_ORDINAL = 19;
        private const ushort BTUICT_ORDINAL = 20;
        private const ushort BTULFD_ORDINAL = 25;
        private const ushort BTULOK_ORDINAL = 26;
        private const ushort BTUMIL_ORDINAL = 30;
        private const ushort BTURST_ORDINAL = 41;
        private const ushort BTUTRG_ORDINAL = 49;
        private const ushort BTUTRM_ORDINAL = 50;
        private const ushort BTUTRU_ORDINAL = 52;
        private const ushort BTUXLT_ORDINAL = 57;
        private const ushort BTUXNF_ORDINAL = 60;
        private const ushort CHIINP_ORDINAL = 62;
        private const ushort BTUICA_ORDINAL = 87;

        private SessionBase Channel => testSessions[0];

        private void Call(ushort ordinal, params ushort[] arguments) =>
            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, ordinal, new List<ushort>(arguments));

        [Fact]
        public void btuech_SetsEcho()
        {
            Reset();

            Call(BTUECH_ORDINAL, 0, 0);
            Assert.False(Channel.GsblInput.EchoEnabled);

            Call(BTUECH_ORDINAL, 0, 1);
            Assert.True(Channel.GsblInput.EchoEnabled);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void btuech_UndefinedChannel()
        {
            Reset();

            Call(BTUECH_ORDINAL, 5, 0);

            Assert.Equal(0xFFF6, mbbsEmuCpuRegisters.AX);
        }

        [Theory]
        [InlineData((short)10, 10, false)]
        [InlineData((short)-10, 10, true)]
        public void btumil_SetsLengthAndWordWrap(short maxinl, int expectedLength, bool expectedWrap)
        {
            Reset();

            Call(BTUMIL_ORDINAL, 0, (ushort)maxinl);

            Assert.Equal(expectedLength, Channel.GsblInput.MaxInputLength);
            Assert.Equal(expectedWrap, Channel.GsblInput.WordWrap);
        }

        [Fact]
        public void btumil_ZeroIsUnlimitedAndKeepsWordWrap()
        {
            Reset();

            Call(BTUMIL_ORDINAL, 0, unchecked((ushort)-10));
            Call(BTUMIL_ORDINAL, 0, 0);

            Assert.Equal(GsblChannelInput.UnlimitedLineLength, Channel.GsblInput.MaxInputLength);
            Assert.True(Channel.GsblInput.WordWrap);
        }

        [Fact]
        public void CharacterSettings()
        {
            Reset();

            Call(BTUBSE_ORDINAL, 0, '_');
            Call(BTULFD_ORDINAL, 0, '*');
            Call(BTUTRM_ORDINAL, 0, '#');
            Call(BTUTRU_ORDINAL, 0, '!');
            Call(BTUXNF_ORDINAL, 0, 0x11, 0x10, 0);

            Assert.Equal((byte)'_', Channel.GsblInput.BackspaceCharacter);
            Assert.Equal((byte)'*', Channel.GsblInput.LinefeedCharacter);
            Assert.Equal((byte)'#', Channel.GsblInput.LineTerminator);
            Assert.Equal((byte)'!', Channel.GsblInput.OutputAbortCharacter);
            Assert.Equal(0x10, Channel.GsblInput.XoffCharacter);
        }

        [Fact]
        public void btulok_LocksInput()
        {
            Reset();

            Call(BTULOK_ORDINAL, 0, 1);
            Assert.True(Channel.InputLockout);

            Call(BTULOK_ORDINAL, 0, 0);
            Assert.False(Channel.InputLockout);
        }

        [Fact]
        public void btuxlt_ChangesTheSharedTranslationTable()
        {
            Reset();

            Call(BTUXLT_ORDINAL, 'a', 'b');

            var table = GsblInput.GetTranslationTable(_serviceResolver.GetService<IGlobalCache>());
            Assert.Equal((byte)'b', table['a']);

            //Restore, since the global cache outlives this test
            table['a'] = (byte)'a';
        }

        [Fact]
        public void bturst_RestoresDefaultsAndDiscardsInput()
        {
            Reset();
            Channel.GsblInput.EchoEnabled = false;
            Channel.GsblInput.Trigger = 4;
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abc"));

            Call(BTURST_ORDINAL, 0);

            Assert.True(Channel.GsblInput.EchoEnabled);
            Assert.Equal(0, Channel.GsblInput.Trigger);
            Assert.Equal(0, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btutrg_SwitchingModesDiscardsInput()
        {
            Reset();
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abc"));

            Call(BTUTRG_ORDINAL, 0, 4);

            Assert.Equal(4, Channel.GsblInput.Trigger);
            Assert.Equal(0, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btutrg_ChangingTheCountKeepsInput()
        {
            Reset();
            Call(BTUTRG_ORDINAL, 0, 4);
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abcdef"));
            Channel.GsblInput.TriggerCount = 2;

            Call(BTUTRG_ORDINAL, 0, 8);

            Assert.Equal(8, Channel.GsblInput.Trigger);
            Assert.Equal(0, Channel.GsblInput.TriggerCount);
            Assert.Equal(6, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btutrg_ZeroReturnsToLineMode()
        {
            Reset();
            Call(BTUTRG_ORDINAL, 0, 4);
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abcdef"));

            Call(BTUTRG_ORDINAL, 0, 0);

            Assert.Equal(0, Channel.GsblInput.Trigger);
            Assert.Equal(0, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btuica_ConsumesWhatItReads()
        {
            Reset();
            var destination = mbbsEmuMemoryCore.AllocateVariable("BUFFER", 0x10);
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abcdef"));

            Call(BTUICA_ORDINAL, 0, destination.Offset, destination.Segment, 4);

            Assert.Equal(4, mbbsEmuCpuRegisters.AX);
            Assert.Equal("abcd", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(destination, 4)));
            Assert.Equal("ef", Encoding.ASCII.GetString(Channel.InputBuffer.ToArray()));

            Call(BTUICA_ORDINAL, 0, destination.Offset, destination.Segment, 4);

            Assert.Equal(2, mbbsEmuCpuRegisters.AX);
            Assert.Equal("ef", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(destination, 2)));
            Assert.Equal(0, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btuict_ReadsOneBlockAtATime()
        {
            Reset();
            var destination = mbbsEmuMemoryCore.AllocateVariable("BUFFER", 0x10);
            Call(BTUTRG_ORDINAL, 0, 4);
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abcdefghij"));

            Call(BTUICT_ORDINAL, 0, destination.Offset, destination.Segment);
            Assert.Equal(4, mbbsEmuCpuRegisters.AX);
            Assert.Equal("abcd", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(destination, 4)));

            Call(BTUICT_ORDINAL, 0, destination.Offset, destination.Segment);
            Assert.Equal(4, mbbsEmuCpuRegisters.AX);
            Assert.Equal("efgh", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(destination, 4)));

            //Short: copied, but left in the buffer
            Call(BTUICT_ORDINAL, 0, destination.Offset, destination.Segment);
            Assert.Equal(0xFFFE, mbbsEmuCpuRegisters.AX);
            Assert.Equal("ij", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(destination, 2)));
            Assert.Equal(2, Channel.InputBuffer.Length);
        }

        [Fact]
        public void btucli_And_btuibw()
        {
            Reset();
            Channel.InputBuffer.Write(Encoding.ASCII.GetBytes("abc"));

            Call(BTUIBW_ORDINAL, 0);
            Assert.Equal(3, mbbsEmuCpuRegisters.AX);

            Call(BTUCLI_ORDINAL, 0);
            Call(BTUIBW_ORDINAL, 0);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void chiinp_CompletesALine()
        {
            Reset();

            foreach (var c in "ab\bc\r")
                Call(CHIINP_ORDINAL, 0, c);

            Assert.Equal("ac", Encoding.ASCII.GetString(Channel.InputBuffer.ToArray()));
            Assert.Equal(HostProcess.Enums.EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE, Channel.GetStatus());
            Assert.Empty(((TestSession)Channel).DrainSentData());
        }
    }
}
