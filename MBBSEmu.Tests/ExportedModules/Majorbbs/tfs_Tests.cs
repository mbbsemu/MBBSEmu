using FluentAssertions;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    [Collection("Non-Parallel")]
    public class tfs_Tests : FileTestBase
    {
        private const int TFSTATE_ORDINAL = 768;
        private const int TFSBUF_ORDINAL = 769;
        private const int TFSPST_ORDINAL = 770;
        private const int TFSOPN_ORDINAL = 773;
        private const int TFSRDL_ORDINAL = 774;
        private const int TFSPFX_ORDINAL = 775;
        private const int TFSABT_ORDINAL = 892;

        private short tfsopn(string filespec)
        {
            var ptr = mbbsEmuMemoryCore.Malloc((ushort)(filespec.Length + 1));
            mbbsEmuMemoryCore.SetArray(ptr, Encoding.ASCII.GetBytes(filespec));
            mbbsEmuMemoryCore.SetByte(ptr + filespec.Length, 0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSOPN_ORDINAL, new List<FarPtr>() { ptr });

            mbbsEmuMemoryCore.Free(ptr);
            return (short)mbbsEmuCpuRegisters.AX;
        }

        private void tfsabt()
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSABT_ORDINAL, new List<ushort>());
        }

        private HostProcess.ExportedModules.Majorbbs.TFStateCodes tfsrdl()
        {
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSRDL_ORDINAL, new List<ushort>());
            return (HostProcess.ExportedModules.Majorbbs.TFStateCodes)mbbsEmuCpuCore.AX;
        }

        private bool tfspfx(string prefix)
        {
            var ptr = mbbsEmuMemoryCore.Malloc((ushort)(prefix.Length + 1));
            mbbsEmuMemoryCore.SetArray(ptr, Encoding.ASCII.GetBytes(prefix));
            mbbsEmuMemoryCore.SetByte(ptr + prefix.Length, 0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSPFX_ORDINAL, new List<FarPtr>() { ptr });

            mbbsEmuMemoryCore.Free(ptr);
            return mbbsEmuCpuRegisters.AX != 0;
        }

        private HostProcess.ExportedModules.Majorbbs.TFStateCodes tfstate => (HostProcess.ExportedModules.Majorbbs.TFStateCodes)mbbsEmuMemoryCore.GetWord(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSTATE_ORDINAL));
        private string tfsbuf => Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSBUF_ORDINAL), stripNull: true));
        private string tfspst => Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuMemoryCore.GetPointer(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSPST_ORDINAL)), stripNull: true));

        [Fact]
        public void tfsopen_bad_file()
        {
            //Reset State
            Reset();

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            tfsopn("file.txt").Should().Be(0);
        }

        [Fact]
        public void tfsopen_open_and_close()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "test\r\nline\r\nanother\r\n";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            var nFiles = tfsopn("file.txt");
            nFiles.Should().Be(1);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBGN);

            tfsabt();

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);
        }

        [Fact]
        public void tfsopen_open_and_line_reading()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "test\r\n\r\nline\r\nanother\r\n";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            var nFiles = tfsopn("file.txt");
            nFiles.Should().Be(1);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBGN);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("test");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("line");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("another");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSEOF);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSEOF);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);
        }

        [Fact]
        public void tfsopen_open_and_prefix_reading()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "Module name:    Awesome module\r\nAnother entry:Whatever\r\nAnother entry:";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            var nFiles = tfsopn("file.txt");
            nFiles.Should().Be(1);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBGN);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("Module name:    Awesome module");

            tfspfx("Not a match:").Should().BeFalse();
            tfspfx("Module name:").Should().BeTrue();
            tfspst.Should().Be("Awesome module");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("Another entry:Whatever");

            tfspfx("Not a match:").Should().BeFalse();
            tfspfx("Another entry:").Should().BeTrue();
            tfspst.Should().Be("Whatever");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be("Another entry:");

            tfspfx("Not a match:").Should().BeFalse();
            tfspfx("Another entry:").Should().BeFalse();
            tfspst.Should().Be("");

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSEOF);
            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);
        }

        [Fact]
        public void tfsopen_open_and_read_huge_line()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "aaaa_aaaa_bbbb_bbbb_cccc_cccc_dddd_dddd_eeee_eeee_" +
                "ffff_ffff_gggg_gggg_hhhh_hhhh_iiii_iiii_jjjj_jjjj_kkkk_kkkk_llll_llll_mmmm_mmmm_" +
                "nnnn_nnnn_oooo_oooo_";

            //Create Text File
            var fileName = CreateTextFile("file.txt", FILE_CONTENTS);

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSDUN);

            var nFiles = tfsopn("file.txt");
            nFiles.Should().Be(1);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBGN);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);

            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN);
            tfsbuf.Should().Be(FILE_CONTENTS.Substring(0, 128));

            tfsabt();
        }

        /// <summary>
        ///     tfsrdl() over a whole file: every line tfsbuf holds until tfsrdl() returns TFSEOF. Expected values are those
        ///     of the original kernels' TFSRDL reading the same bytes through their run-time library's text-mode read
        ///     (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical results): every CR is dropped, a ^Z ends the
        ///     file, a line longer than 128 characters goes on at the next call, and only ASCII white space is removed
        ///     from the end of a line.
        /// </summary>
        [Theory]
        [InlineData("one\r\ntwo\r\n", new string[] { "one", "two" })]
        [InlineData("ab\rcd\r\nef\n", new string[] { "abcd", "ef" })]
        [InlineData("one\ntwo\n", new string[] { "one", "two" })]
        [InlineData("one\r\ntwo", new string[] { "one", "two" })]
        [InlineData("", new string[0])]
        [InlineData("one\r\ntw\u001Ao\r\nthree\r\n", new string[] { "one", "tw" })]
        [InlineData("\u001Aone\r\n", new string[0])]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmn\r\nnext\r\n", new string[] { "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwx", "yzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuv", "wxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmn", "next" })]
        [InlineData("CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\r\nx\r\n", new string[] { "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC", "", "x" })]
        [InlineData("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\r\n", new string[] { "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" })]
        [InlineData("EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE\rEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE\rEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE\r\n", new string[] { "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE", "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE" })]
        [InlineData("abc \t\v\f \r\n  lead \u00A0\r\nhi\u0085\r\n", new string[] { "abc", "  lead \u00A0", "hi\u0085" })]
        [InlineData("\u00C9\u00E9\u00B3\u00FF\u0080\r\n", new string[] { "\u00C9\u00E9\u00B3\u00FF\u0080" })]
        [InlineData("\r\n\r\n  \r\nx\r\n", new string[] { "", "", "", "x" })]
        [InlineData("ab\0cd  \r\nnext\r\n", new string[] { "ab", "next" })]
        [InlineData("ab  \0cd\r\nx\r\n", new string[] { "ab", "x" })]
        [InlineData("one\r\n\u001A", new string[] { "one" })]
        public void tfsrdl_MatchesKernel(string contents, string[] expectedLines)
        {
            Reset();

            File.WriteAllBytes(Path.Join(mbbsModule.ModulePath, "file.txt"), Encoding.Latin1.GetBytes(contents));
            tfsopn("file.txt").Should().Be(1);
            tfsrdl().Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSBOF);

            var lines = new List<string>();
            while (tfsrdl() == HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSLIN)
                lines.Add(Encoding.Latin1.GetString(mbbsEmuMemoryCore.GetString(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSBUF_ORDINAL), stripNull: true)));

            tfstate.Should().Be(HostProcess.ExportedModules.Majorbbs.TFStateCodes.TFSEOF);
            lines.Should().Equal(expectedLines);

            tfsabt();
        }

        /// <summary>
        ///     tfspfx() on a line in tfsbuf: the return value and where tfspst points afterwards (an offset into tfsbuf,
        ///     -1 for NULL). Expected values are those of the original kernels' TFSPFX on the same line and prefix (The
        ///     Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical results): the prefix ignores the case of ASCII
        ///     letters only, and only spaces are skipped after it.
        /// </summary>
        [Theory]
        [InlineData("Module Name: X", "module name:", 1, 13)]
        [InlineData("module name: X", "MODULE NAME:", 1, 13)]
        [InlineData("ABC\tW", "ABC", 1, 3)]
        [InlineData("ABC  W", "ABC", 1, 5)]
        [InlineData("ABC", "ABC", 0, 3)]
        [InlineData("ABC   ", "ABC", 0, 6)]
        [InlineData("ABD", "ABC", 0, -1)]
        [InlineData("AB", "ABC", 0, -1)]
        [InlineData("abc W", "ABC", 1, 4)]
        [InlineData("ABCW", "ABC", 1, 3)]
        [InlineData("", "", 0, 0)]
        [InlineData("X", "", 1, 0)]
        [InlineData("  X", "", 1, 2)]
        [InlineData("ABC W  ", "ABC", 1, 4)]
        [InlineData("ABC W\t", "ABC", 1, 4)]
        [InlineData("ABC \tW", "ABC", 1, 4)]
        [InlineData("ABC\t", "ABC", 1, 3)]
        [InlineData("ABC\nW", "ABC", 1, 3)]
        [InlineData("ABC\rW", "ABC", 1, 3)]
        [InlineData("ABC\vW", "ABC", 1, 3)]
        [InlineData("ABC\fW", "ABC", 1, 3)]
        [InlineData("\u00E9X Y", "\u00E9X", 1, 3)]
        [InlineData("\u00C9X Y", "\u00E9x", 0, -1)]
        [InlineData("\u00E9X Y", "\u00C9x", 0, -1)]
        [InlineData("[X] Y", "{x}", 0, -1)]
        [InlineData("{X} Y", "[x]", 0, -1)]
        [InlineData("@X Y", "`x", 0, -1)]
        [InlineData("^X Y", "~x", 0, -1)]
        [InlineData("_X Y", "\u007Fx", 0, -1)]
        [InlineData("A Y", "a", 1, 2)]
        [InlineData("Z Y", "z", 1, 2)]
        [InlineData("1 Y", "1", 1, 2)]
        [InlineData("\u00C9X Y", "\u00E9X", 0, -1)]
        [InlineData("\u00E9X Y", "?X", 0, -1)]
        public void tfspfx_MatchesKernel(string line, string prefix, int expectedResult, int expectedOffset)
        {
            Reset();

            var tfsbufPointer = GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSBUF_ORDINAL);
            mbbsEmuMemoryCore.SetArray(tfsbufPointer, Encoding.Latin1.GetBytes(line + "\0"));
            var prefixPointer = mbbsEmuMemoryCore.Malloc((ushort)(prefix.Length + 1));
            mbbsEmuMemoryCore.SetArray(prefixPointer, Encoding.Latin1.GetBytes(prefix + "\0"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TFSPFX_ORDINAL, new List<FarPtr>() { prefixPointer });

            mbbsEmuMemoryCore.Free(prefixPointer);
            mbbsEmuCpuRegisters.AX.Should().Be((ushort)expectedResult);
            var tfspstPointer = mbbsEmuMemoryCore.GetPointer(GetOrdinalAddress(HostProcess.ExportedModules.Majorbbs.Segment, TFSPST_ORDINAL));
            if (expectedOffset < 0)
                tfspstPointer.Should().Be(FarPtr.Empty);
            else
                tfspstPointer.Should().Be(tfsbufPointer + expectedOffset);
        }
    }
}
