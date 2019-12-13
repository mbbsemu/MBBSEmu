using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Telnet
{
    public enum EnumIacVerbs : byte
    {
        WILL = 251,
        DO = 252,
        WONT = 253,
        DONT = 254
    }
}
