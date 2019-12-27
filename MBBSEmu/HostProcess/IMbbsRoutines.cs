using System.Collections.Generic;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess
{
    public interface IMbbsRoutines
    {
        void ProcessSessionState(UserSession session, Dictionary<string, MbbsModule> modules);
        void DisplayWelcomeScreen(UserSession session);
        void DisplayLoginUsername(UserSession session);
        void AuthenticatingUsername(UserSession session);
        void DisplayLoginPassword(UserSession session);
        void AuthenticatingPassword(UserSession session);
        void MainMenuDisplay(UserSession session, Dictionary<string, MbbsModule> modules);
        void MainMenuInput(UserSession session, Dictionary<string, MbbsModule> modules);
        void ConfirmLogoffDisplay(UserSession session);
        void ConfirmLogoffInput(UserSession session);
        void LoggingOff(UserSession session);
    }
}