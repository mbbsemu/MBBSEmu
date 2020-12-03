using System;
using System.Linq;
using MBBSEmu.CPU;
using MBBSEmu.Disassembler;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Memory;

namespace MBBSEmu.Exe
{
    public class ExeFile
    {
        public MZFile File;

        public IMemoryCore Memory;
        public ICpuCore Cpu;

        public ExeFile(string file)
        {

        }

        public ExeFile(MZFile file)
        {
            File = file;
            Memory = new MemoryCore();
            Cpu = new CpuCore();

            Load();
        }

        private void Load()
        {
            ApplyRelocation();
            LoadSegments();
        }

        private void ApplyRelocation()
        {
            //Get the Code Segment in the MZFile
            var codeSegment = File.Segments.First(x => x.Flag == (ushort) EnumSegmentFlags.Code).Data;

            //For the time being, only handle the 1st relo for the data segment
            foreach (var relo in File.RelocationRecords)
            {
                //Data Segment is always the last one
                Array.Copy(BitConverter.GetBytes((ushort) File.Segments.Count), 0, codeSegment, relo.Offset,
                    sizeof(ushort));
            }
        }

        private void LoadSegments()
        {
            for (ushort i = 1; i <= File.Segments.Count; i++)
            {
                var seg = File.Segments[i - 1];
                switch (seg.Flag)
                {
                    case (ushort)EnumSegmentFlags.Code:
                    {
                        Memory.AddSegment(seg);
                        break;
                    }
                    case (ushort)EnumSegmentFlags.Data:
                    {
                        Memory.AddSegment(i);
                        Memory.SetArray(i, 0, seg.Data);
                        break;
                    }
                }
            }
        }
    }
}
