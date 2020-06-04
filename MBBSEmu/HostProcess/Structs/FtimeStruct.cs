using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog.LayoutRenderers.Wrappers;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     ftime Struct (IO.H)
    ///
    ///     Defined as 4 bytes (32bits)
    /// </summary>
    public class FtimeStruct
    {
        public byte tsec
        {
            set
            {
                var dataValue = BitConverter.ToUInt32(Data);
                dataValue &= 0x7FFFFFF; //Zero out bits 0-4
                dataValue |= (uint)value << 27;
                Data = BitConverter.GetBytes(dataValue);
            }
        }

        public byte min
        {
            set
            {
                var dataValue = BitConverter.ToUInt32(Data);
                dataValue &= 0xF81FFFFF; //Zero out bits 5-10
                value &= 0x3F; //Only keep first 6 bits
                dataValue |= (uint)value << 21;
                Data = BitConverter.GetBytes(dataValue);
            }
        }

        public byte hours
        {
            set
            {
                var dataValue = BitConverter.ToUInt32(Data);
                dataValue &= 0xFFE0FFFF; //Zero out bits 11-15
                value &= 0x1F; //Only keep first 5 bits
                dataValue |= (uint)value << 16;
                Data = BitConverter.GetBytes(dataValue);
            }
        }

        public byte day
        {
            set
            {
                var dataValue = BitConverter.ToUInt32(Data);
                dataValue &= 0xFFFE0FFF; //Zero out bits 16-20
                value &= 0x1F; //Only keep first 5 bits
                dataValue |= (uint)value << 11;
                Data = BitConverter.GetBytes(dataValue);
            }
        }

        public byte month
        {
            set
            {
                var dataValue = BitConverter.ToUInt32(Data);
                dataValue &= 0xFFFFF87F; //Zero out bits 21-24
                value &= 0xF; //Only keep first 4 bits
                dataValue |= (uint)value << 6;
                Data = BitConverter.GetBytes(dataValue);
            }
        }

        public byte year;

        public byte[] Data = new byte[Size];

        public const ushort Size = 4;
    }
}
