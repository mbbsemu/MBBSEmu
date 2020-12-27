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
        public FarPtr read => new FarPtr(((ReadOnlySpan<byte>)Data).Slice(9, 4));
        public FarPtr write => new FarPtr(((ReadOnlySpan<byte>)Data).Slice(13, 4));
        public FarPtr xferdone => new FarPtr(((ReadOnlySpan<byte>)Data).Slice(17, 4));
        public FarPtr abort => new FarPtr(((ReadOnlySpan<byte>)Data).Slice(21, 4));

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
