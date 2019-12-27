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
        void InputtingUsername(UserSession session);
        void DisplayLoginPassword(UserSession session);
        void InputtingPassword(UserSession session);
        void MainMenuDisplay(UserSession session, Dictionary<string, MbbsModule> modules);
        void InputtingMainMenuSelection(UserSession session, Dictionary<string, MbbsModule> modules);
        void ConfirmLogoffDisplay(UserSession session);
        void InputtingConfirmLogoff(UserSession session);
        void LoggingOff(UserSession session);
    }
}