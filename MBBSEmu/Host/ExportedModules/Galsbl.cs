using System;
using System.Collections.Generic;
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
            
        }
    }
}
