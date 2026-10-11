using System;
using MBBSEmu.Disassembler;
using MBBSEmu.Logging;
using Xunit;

namespace MBBSEmu.Tests.Disassembler
{
    /// <summary>
    ///     Regression tests for NE (New Executable) file parsing, in particular
    ///     the Entry Table, which is used to resolve named exports to a
    ///     segment:offset pair during module relocation/import resolution.
    /// </summary>
    public class NEFile_Tests
    {
        private const ushort WindowsHeaderOffset = 0x40;

        /// <summary>
        ///     Builds a minimal, synthetic NE file containing a single
        ///     "movable" Entry Table bundle (segment marker 0xFF) with one
        ///     entry, so we can assert the parsed <see cref="MBBSEmu.Disassembler.Artifacts.Entry"/>
        ///     gets a correct, non-zero Ordinal.
        ///
        ///     Segment/Resource/Resident-Name/Module-Reference/Imported-Name
        ///     tables are all intentionally empty (entry counts of 0) so the
        ///     test only exercises Entry Table + Non-Resident Name Table
        ///     parsing.
        /// </summary>
        private static byte[] BuildMinimalNeFileWithMovableEntry(byte segmentNumber, ushort offset)
        {
            const int entryTableRelativeOffset = 0x3F; // right after the 0x3F-byte NE header
            var entryTableOffset = WindowsHeaderOffset + entryTableRelativeOffset; // 127

            // Entry table bytes: [entryCount=1][segment=0xFF][flag][pad][pad][segmentNumber][offsetLo][offsetHi]
            var entryTableBytes = new byte[]
            {
                0x01,           // entryCount (also doubles as the "bundle count" hint read before the loop)
                0xFF,           // 0xFF => movable segment marker
                0x03,           // Flag (arbitrary "exported" style flag)
                0xCD, 0x3F,     // padding / historically "INT 3Fh" opcode bytes, unused by the parser
                segmentNumber,  // actual segment number for this movable entry
                (byte)(offset & 0xFF),
                (byte)((offset >> 8) & 0xFF)
            };

            var nonResidentNameTableOffset = (uint)(entryTableOffset + entryTableBytes.Length);

            var fileLength = nonResidentNameTableOffset + 1; // +1 byte of headroom, table length is 0
            var file = new byte[fileLength];

            // --- MZ (DOS) Header ---
            file[0] = (byte)'M';
            file[1] = (byte)'Z';
            file[0x18] = 0x40; // signals "extended header present" per NEFile.Load()
            BitConverter.GetBytes(WindowsHeaderOffset).CopyTo(file, 0x3C);

            // --- NE (Windows) Header, 0x3F bytes starting at WindowsHeaderOffset ---
            var h = WindowsHeaderOffset;
            file[h + 0x00] = (byte)'N';
            file[h + 0x01] = (byte)'E';
            BitConverter.GetBytes((ushort)entryTableRelativeOffset).CopyTo(file, h + 0x04); // EntryTableOffset (relative)
            BitConverter.GetBytes((ushort)entryTableBytes.Length).CopyTo(file, h + 0x06);   // EntryTableLength
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x1C);  // SegmentTableEntries
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x1E);  // ModuleReferenceTableEntries
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x20);  // NonResidentNameTableLength
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x22);  // SegmentTableOffset (relative, unused - 0 entries)
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x24);  // ResourceTableOffset (relative, unused)
            // ResidentNameTableOffset (relative): point at the trailing zero-length
            // headroom byte so the "end of names" check (length byte == 0) fires
            // on the very first iteration.
            BitConverter.GetBytes((ushort)(entryTableRelativeOffset + entryTableBytes.Length)).CopyTo(file, h + 0x26);
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x28);  // ModuleReferenceTableOffset (relative, unused - 0 entries)
            BitConverter.GetBytes((ushort)0).CopyTo(file, h + 0x2A);  // ImportedNamesTableOffset (relative, unused - 0 entries)
            BitConverter.GetBytes(nonResidentNameTableOffset).CopyTo(file, h + 0x2C); // NonResidentNameTableOffset (ABSOLUTE)

            // --- Entry Table ---
            entryTableBytes.CopyTo(file, entryTableOffset);

            return file;
        }

        [Fact]
        public void Load_MovableEntryTableBundle_AssignsNonZeroOrdinal()
        {
            var fileContent = BuildMinimalNeFileWithMovableEntry(segmentNumber: 1, offset: 0x1234);

            var neFile = new NEFile(new MessageLogger(), fullFilePath: "test.dll", data: fileContent);

            var entry = Assert.Single(neFile.EntryTable);

            // Before the fix, movable entries never had their Ordinal assigned
            // and were left at the default value of 0, which meant named-export
            // lookups (e.g. MbbsHost.First) could never find them.
            Assert.Equal(1, entry.Ordinal);
            Assert.Equal(1, entry.SegmentNumber);
            Assert.Equal(0x1234, entry.Offset);
        }
    }
}
