using System;
using System.Collections.Generic;
using MBBSEmu.Disassembler.Artifacts;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.Disassembler
{
    public class MZFile
    {
        private readonly MZHeader _mzHeader;
        private readonly string _exeFile;
        private readonly byte[] _exeFileData;

        public readonly List<Segment> Segments;

        public readonly List<IntPtr16> RelocationRecords;

        public IntPtr16 StartingPointer = new IntPtr16(1 + 0x10, 0); //PSP+10h for Segment

        public MZFile(string exeFile)
        {
            Segments = new List<Segment>();
            RelocationRecords = new List<IntPtr16>();

            _exeFile = exeFile;
            _exeFileData = File.ReadAllBytes(_exeFile);

            _mzHeader = new MZHeader(_exeFileData);

            Load();
        }

        private void Load()
        {
            //Get EXE Contents
            var contentSpan = new ReadOnlySpan<byte>(_exeFileData);
            var segment = new Segment
            {
                Data = contentSpan.Slice(_mzHeader.HeaderSize, _mzHeader.ProgramSize).ToArray(),
                Flag = (ushort) EnumSegmentFlags.Code
            };
            Segments.Add(segment);


            //Parse Relocation Records
            LoadRelocationRecords(); 
        }

        /// <summary>
        ///     Starting at the 
        /// </summary>
        private void LoadRelocationRecords()
        {
            for (var i = 0; i < _mzHeader.RelocationEntries; i++)
            {
                var relocationAddress = _mzHeader.RelocationOffset + (4 * i);

                var offset = BitConverter.ToUInt16(_exeFileData, relocationAddress);
                var segment = BitConverter.ToUInt16(_exeFileData, relocationAddress+2);

                RelocationRecords.Add(new IntPtr16(segment, offset));
            }
        }


    }
}
