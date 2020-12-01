using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.CPU.Interrupts
{
    public interface IInterruptHandler
    {
        void Handle();
    }
}
