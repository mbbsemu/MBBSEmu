using System.Collections.Generic;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess
{
    public interface IMbbsRoutines
    {
        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        void ShowLogin(UserSession session);

        void DisplayLoginUsername(UserSession session);
        void AuthenticatingUsername(UserSession session);
        void DisplayLoginPassword(UserSession session);
        void AuthenticatingPassword(UserSession session);
        void MainMenuDisplay(UserSession session, Dictionary<string, MbbsModule> modules);
        void MainMenuInput(UserSession session, Dictionary<string, MbbsModule> modules);
    }
}