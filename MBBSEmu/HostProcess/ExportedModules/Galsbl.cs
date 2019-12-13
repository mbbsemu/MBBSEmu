using System;
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
        public Galsbl(CpuRegisters cpuRegisters, MbbsModule module) : base(cpuRegisters, module)
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
            const string registrationNumber = "97771457\0";
            Memory.SetArray((ushort)EnumHostSegments.Bturno, 0, Encoding.Default.GetBytes(registrationNumber));

            return (ushort) EnumHostSegments.Bturno;
        }

        /// <summary>
        ///     Report the amount of space (number of bytes) available in the output buffer
        ///     Since we're not using a dialup terminal or any of that, we'll just set it to ushort.MaxValue
        ///
        ///     Signature: int btuoba(int chan)
        ///     Result: AX == bytes available
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTUOBA", Ordinal = 36)]
        public ushort btuoba()
        {
            Registers.AX = ushort.MaxValue;

            return 0;
        }

        /// <summary>
        ///     Set the input byte trigger quantity (used in conjunction with btuict())
        ///
        ///     Signature: int btutrg(int chan,int nbyt)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTUTRG", Ordinal = 49)]
        public ushort btutrg()
        {
            //TODO -- Set callback for how characters should be processed

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Inject a status code into a channel
        /// 
        ///     Signature: int btuinj(int chan,int status)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTUINJ", Ordinal = 21)]
        public ushort butinj()
        {
            //TODO -- Figure out what to do with these status codes
            var channel = GetParameter(0);
            var status = GetParameter(1);
            Memory.SetWord((ushort)EnumHostSegments.Status, 0, status);

            Registers.AX = 0;

            return 0;
        }

        /// <summary>
        ///     Set XON/XOFF characters, select page mode
        ///
        ///     Signature: int btuxnf(int chan,int xon,int xoff,...)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTUXNF", Ordinal = 60)]
        public ushort btuxnf()
        {
            //Ignore this, we won't deal with XON/XOFF
            Registers.AX = 0;
            return 0;
        }

        /// <summary>
        ///     Set screen-pause character
        ///     Pauses the screen when in the output stream
        ///
        ///     Puts the screen in screen-pause mode
        ///     Signature: int err=btupbc(int chan, char pausch)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        [ExportedFunction(Name = "_BTUPBC", Ordinal = 39)]
        public ushort btupbc()
        {
            //TODO -- Handle this?

            Registers.AX = 0;
            return 0;
        }
    }
}
