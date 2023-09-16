using System.Collections.Generic;

namespace MBBSEmu.Logging.Targets
{
    public interface IQueueTarget
    {
        object[] Dequeue();
        IList<object[]> DequeueAll();
        void Clear();
    }
}
