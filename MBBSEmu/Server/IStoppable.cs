using MBBSEmu.Session;

namespace MBBSEmu.Server
{
    public interface IStoppable
    {
        /// <summary>
        ///     Requests the object close any persistent resources in a clean manner, such as
        ///     Threads or Timers.
        /// </summary>
        void Stop();
    }
}
