using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MBBSEmu.Logging.Targets
{
    public interface ILoggingTarget
    {
        public void Write(params object[] logEntry);
    }
}
