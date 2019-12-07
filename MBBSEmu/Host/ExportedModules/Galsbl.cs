using System.Text;
using MBBSEmu.CPU;
using MBBSEmu.Module;

namespace MBBSEmu.Host.ExportedModules
{
    /// <summary>
    ///     Class which defines functions &amp; properties that are part of the Galacticomm
    ///     Global Software Breakout Library (GALGSBL.H). 
    /// </summary>
    public class Galsbl : ExportedModuleBase
    {
        public Galsbl(CpuCore cpuCore, MbbsModule module) : base(cpuCore, module)
        {
            Memory.AddSegment((ushort)EnumHostSegments.Bturno);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        [ExportedModule(Name = "_BTURNO", Ordinal = 72, ExportedModuleType = EnumExportedModuleType.Value)]
        public ushort bturno()
        {
            const string registrationNumber = "12345678\0";
            Memory.SetArray((ushort)EnumHostSegments.Bturno, 0, Encoding.ASCII.GetBytes(registrationNumber));

            return (ushort) EnumHostSegments.Bturno;
        }
    }
}
