using MBBSEmu.Extensions;
using MBBSEmu.Session.Enums;
using Xunit;

namespace MBBSEmu.Tests.Extensions
{
    public class EnumSessionState_Tests
    {
        [Theory]
        [InlineData(EnumSessionState.EnteringFullScreenEditor, "", "", true, true)]
        [InlineData(EnumSessionState.ExitingFullScreenDisplay, "", "", true, true)]
        [InlineData(EnumSessionState.InFullScreenDisplay, "", "", true, true)]
        [InlineData(EnumSessionState.InFullScreenEditor, "", "", true, true)]
        [InlineData(EnumSessionState.EnteringModule, "", "", true, true)]
        [InlineData(EnumSessionState.RloginEnteringModule, "", "", true, true)]
        [InlineData(EnumSessionState.InModule, "", "", true, true)]
        [InlineData(EnumSessionState.LoginRoutines, "", "Main Menu", true, false)]
        [InlineData(EnumSessionState.MainMenuDisplay, "", "Main Menu", true, false)]
        [InlineData(EnumSessionState.MainMenuInput, "", "Main Menu", true, false)]
        [InlineData(EnumSessionState.MainMenuInputDisplay, "", "Main Menu", true, false)]
        [InlineData(EnumSessionState.ExitingModule, "", "Main Menu", true, false)]
        [InlineData(EnumSessionState.SignupUsernameInput, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupUsernameDisplay, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupEmailDisplay, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupEmailInput, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupPasswordDisplay, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupPasswordInput, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupPasswordConfirm, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupPasswordConfirmDisplay, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.SignupPasswordConfirmInput, "<<New User>>", "New User Sign-up", false, false)]
        [InlineData(EnumSessionState.Negotiating, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.Unauthenticated, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.LoginPasswordDisplay, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.LoginPasswordInput, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.LoginUsernameDisplay, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.LoginUsernameInput, "<<Logging In>>", "User Login", false, false)]
        [InlineData(EnumSessionState.LoggingOffDisplay, "", "Logging Off", true, false)]
        [InlineData(EnumSessionState.LoggingOffProcessing, "", "Logging Off", true, false)]
        [InlineData(EnumSessionState.LoggedOff, "", "Logging Off", true, false)]
        [InlineData(EnumSessionState.ConfirmLogoffInput, "", "Logging Off", true, false)]
        [InlineData(EnumSessionState.ConfirmLogoffDisplay, "", "Logging Off", true, false)]
        [InlineData(EnumSessionState.Disconnected, "*UNKNOWN*", "*UNKNOWN*", false, false)]
        public void EnumSessionState_Test(EnumSessionState srcSessionState, string expUserName, string expUserOptionSelected, bool expUserSession, bool expModuleSession)
        {
            var srcSessionInfo = srcSessionState.GetSessionState();

            //Verify Results
            Assert.Equal(expUserName, srcSessionInfo.userName);
            Assert.Equal(expUserOptionSelected, srcSessionInfo.UserOptionSelected);
            Assert.Equal(expUserSession, srcSessionInfo.userSession);
            Assert.Equal(expModuleSession, srcSessionInfo.moduleSession);
        }
    }
}
