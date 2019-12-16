using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess
{
    public interface IMbbsHost
    {
        /// <summary>
        ///     Starts the MbbsHost Worker Thread
        /// </summary>
        void Start();

        /// <summary>
        ///     Stops the MbbsHost worker thread
        /// </summary>
        void Stop();

        /// <summary>
        ///     Adds a Module to the MbbsHost Process and sets it up for use
        /// </summary>
        /// <param name="module"></param>
        void AddModule(MbbsModule module);

        /// <summary>
        ///     Assigns a Channel # to a session and adds it to the Channel Dictionary
        /// </summary>
        /// <param name="session"></param>
        void AddSession(UserSession session);

        /// <summary>
        ///     Removes a Channel # from the Channel Dictionary
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        bool RemoveSession(ushort channel);
    }
}