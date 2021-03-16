using System;
using System.Collections.Generic;
using MBBSEmu.Disassembler.Artifacts;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.Disassembler
{
    public class MZFile
    {
        public MZHeader Header { get; init; }
        private readonly string _exeFile;
        public byte[] ProgramData { get; init; }
        public List<FarPtr> RelocationRecords { get; init; }

        public MZFile(string exeFile)
        {
            RelocationRecords = new List<FarPtr>();

            _exeFile = exeFile;
            var exeFileData = File.ReadAllBytes(_exeFile);

            Header = new MZHeader(exeFileData);

            LoadRelocationRecords(exeFileData);
            ProgramData = LoadProgramData(exeFileData);
        }

        private byte[] LoadProgramData(byte[] exeFileData)
        {
            //Get EXE Contents
            var contentSpan = new ReadOnlySpan<byte>(exeFileData);
            return contentSpan.Slice(Header.HeaderSize, Header.ProgramSize).ToArray();
        }

        /// <summary>
        ///     Applies Relocation Records to the EXE data at load time
        /// </summary>
        private void LoadRelocationRecords(byte[] exeFileData)
        {
            for (var i = 0; i < Header.RelocationEntries; i++)
            {
                var relocationAddress = Header.RelocationOffset + (4 * i);

                var offset = BitConverter.ToUInt16(exeFileData, relocationAddress);
                var segment = BitConverter.ToUInt16(exeFileData, relocationAddress + 2);

                RelocationRecords.Add(new FarPtr(segment, offset));
            }
        }
    }
}
