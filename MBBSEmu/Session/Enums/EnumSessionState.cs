using MBBSEmu.Session.Attributes;

namespace MBBSEmu.Session.Enums
{
    public enum EnumSessionState
    {
        Negotiating,

        /// <summary>
        ///     Initial State for all Sessions
        /// </summary>
        Unauthenticated,

        /// <summary>
        ///     Displaying the Username Prompt for Input
        /// </summary>
        LoginUsernameDisplay,

        /// <summary>
        ///     User is being prompted for their Username
        /// </summary>
        LoginUsernameInput,

        /// <summary>
        ///     Displaying the Password Prompt for Input
        /// </summary>
        LoginPasswordDisplay,

        /// <summary>
        ///     User is being prompted for their Password
        /// </summary>
        LoginPasswordInput,

        /// <summary>
        ///     User is currently seeing Login Routines from Modules
        /// </summary>
        LoginRoutines,

        /// <summary>
        ///     User is at the Main Menu
        /// </summary>
        [DoGlobals]
        MainMenuDisplay,
        MainMenuInputDisplay,
        
        [DoGlobals]
        MainMenuInput,

        /// <summary>
        ///     User is Signing up, prompted for Username
        /// </summary>
        SignupUsernameDisplay,

        SignupUsernameInput,

        /// <summary>
        ///     User is Signing up, prompted for Password
        /// </summary>
        SignupPasswordDisplay,

        SignupPasswordInput,

        SignupPasswordConfirmDisplay,
        SignupPasswordConfirmInput,

        SignupEmailDisplay,
        SignupEmailInput,

        /// <summary>
        ///     User is Signing up, prompted for Password Confirmation
        /// </summary>
        SignupPasswordConfirm,

        /// <summary>
        ///     User is Entering Module from Rlogin session
        /// </summary>
        [DoGlobals]
        RloginEnteringModule,

        /// <summary>
        ///     User is Entering Module (initial routines running)
        /// </summary>
        [DoGlobals]
        EnteringModule,

        /// <summary>
        ///     User is In Module (waiting for input)
        /// </summary>
        [DoGlobals]
        InModule,

        /// <summary>
        ///     User is Existing Module
        /// </summary>
        [DoGlobals]
        ExitingModule,

        [DoGlobals]
        ConfirmLogoffDisplay,

        [DoGlobals]
        ConfirmLogoffInput,

        /// <summary>
        ///     User is in the process of Logging Off
        /// </summary>
        LoggingOffDisplay,

        LoggingOffProcessing,

        /// <summary>
        ///     User is logged off, dispose of Session
        /// </summary>
        LoggedOff,

        EnteringFullScreenDisplay,

        InFullScreenDisplay,

        EnteringFullScreenEditor,

        InFullScreenEditor,

        ExitingFullScreenDisplay,

        ExitingFullScreenEditor,

        Disconnected
    }
}
