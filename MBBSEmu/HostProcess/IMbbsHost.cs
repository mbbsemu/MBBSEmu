using MBBSEmu.Date;
using MBBSEmu.Module;
using MBBSEmu.Server;
using MBBSEmu.Session;
using NLog;
using System.Collections.Generic;
using System.Threading;

namespace MBBSEmu.HostProcess
{
    public interface IMbbsHost : IStoppable
    {
        /// <summary>
        ///     Starts the MbbsHost Worker Thread
        /// </summary>
        /// <param name="moduleConfigurations"></param>
        void Start(List<ModuleConfiguration> moduleConfigurations);

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

        /// <summary>
        ///     Schedules nightly shutdown to occur immediately
        /// </summary>
        /// <param name="restartCompletionEvent">EventWaitHandle to set when restart has completed</param>
        public void ScheduleNightlyShutdown(EventWaitHandle restartCompletionEvent);

        /// <summary>
        ///     Waits until the processing thread exits. Useful for tests
        /// </summary>
        public void WaitForShutdown();

        /// <summary>
        ///     Generates API Report
        /// </summary>
        public void GenerateAPIReport();

        public ILogger Logger { get; init; }

        public IClock Clock { get; init; }
    }
}
