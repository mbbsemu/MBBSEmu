using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;

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
        void AddSession(SessionBase session);

        /// <summary>
        ///     Removes a Channel # from the Channel Dictionary
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        bool RemoveSession(ushort channel);

        /// <summary>
        ///     Returns the List of Current User Sessions
        /// </summary>
        /// <returns></returns>
        public IList<SessionBase> GetUserSessions();

        /// <summary>
        ///     Gets the Registered instance of the specified Module within the MBBS Host Process
        /// </summary>
        /// <param name="uniqueIdentifier"></param>
        /// <returns></returns>
        public MbbsModule GetModule(string uniqueIdentifier);
    }
}