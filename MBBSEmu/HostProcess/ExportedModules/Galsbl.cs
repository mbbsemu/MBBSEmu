using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.Attributes;
using MBBSEmu.Memory;
using MBBSEmu.Module;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions &amp; properties that are part of the Galacticomm
    ///     Global Software Breakout Library (GALGSBL.H). 
    /// </summary>
    [ExportedModule(Name = "GALGSBL")]
    public class Galsbl : ExportedModuleBase
    {
        public Galsbl(IMemoryCore memoryCore, CpuRegisters cpuRegisters, MbbsModule module) : base(memoryCore, cpuRegisters, module)
        {
            if(!Memory.HasSegment((ushort)EnumHostSegments.Bturno))
                Memory.AddSegment((ushort) EnumHostSegments.Bturno);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTURNO", Ordinal = 72)]
        public ushort bturno()
        {
            const string registrationNumber = "12345678\0";
            Memory.SetArray((ushort)EnumHostSegments.Bturno, 0, Encoding.ASCII.GetBytes(registrationNumber));

            return (ushort) EnumHostSegments.Bturno;
        }
    }
}
