using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Agent Struct defined in GCSPSRV.H
    ///
    ///     Holds Agent Information for the Galacticomm Client/Server
    /// </summary>
    public class AgentStruct
    {
        public ReadOnlySpan<byte> appid => ((ReadOnlySpan<byte>) Data).Slice(0, 9);
        public IntPtr16 read => new IntPtr16(((ReadOnlySpan<byte>)Data).Slice(9, 4));
        public IntPtr16 write => new IntPtr16(((ReadOnlySpan<byte>)Data).Slice(13, 4));
        public IntPtr16 xferdone => new IntPtr16(((ReadOnlySpan<byte>)Data).Slice(17, 4));
        public IntPtr16 abort => new IntPtr16(((ReadOnlySpan<byte>)Data).Slice(21, 4));

        public byte[] Data;

        public const ushort Size = 25;

        public AgentStruct()
        {
            Data = new byte[Size];
        }

        public AgentStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }
    }
}
