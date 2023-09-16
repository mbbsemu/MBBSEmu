using System.Collections.Generic;

namespace MBBSEmu.Logging.Targets
{
    public interface IQueueTarget
    {
        string Dequeue();
        IList<string> DequeueAll();
        void Clear();
    }
}
