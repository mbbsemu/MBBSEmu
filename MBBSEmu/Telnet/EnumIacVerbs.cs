using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Telnet
{
    public enum EnumIacVerbs : byte
    {
        WILL = 251,
        WONT = 252,
        DO = 253,
        DONT = 254,
        None = 0xFF
    }
}
