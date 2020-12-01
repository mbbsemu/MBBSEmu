using MBBSEmu.CPU;
using MBBSEmu.Disassembler;
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

            Memory.AddSegment(File.Segments[0]);
        }

        private void Load()
        {

        }
    }
}
