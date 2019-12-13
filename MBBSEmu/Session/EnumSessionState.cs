namespace MBBSEmu.Session
{
    public enum EnumSessionState
    {
        /// <summary>
        ///     Initial State for all Sessions
        /// </summary>
        Unauthenticated,

        /// <summary>
        ///     User is being prompted for their Username
        /// </summary>
        AuthenticatingUsername,

        /// <summary>
        ///     User is being prompted for their Password
        /// </summary>
        AuthenticatingPassword,

        /// <summary>
        ///     User is Logging in / being Authenticated
        /// </summary>
        LoggingIn,

        /// <summary>
        ///     User is currently seeing Login Routines from Modules
        /// </summary>
        LoginRoutines,

        /// <summary>
        ///     User is at the Main Menu
        /// </summary>
        MainMenu,

        /// <summary>
        ///     User is Signing up, prompted for Username
        /// </summary>
        SignupUsername,

        /// <summary>
        ///     User is Signing up, prompted for Password
        /// </summary>
        SignupPassword,

        /// <summary>
        ///     User is Signing up, prompted for Password Confirmation
        /// </summary>
        SignupConfirmPassword,

        /// <summary>
        ///     User is Entering Module (initial routines running)
        /// </summary>
        EnteringModule,

        /// <summary>
        ///     User is In Module (waiting for input)
        /// </summary>
        InModule,

        /// <summary>
        ///     User is Existing Module
        /// </summary>
        ExitingModule,

        /// <summary>
        ///     User is in the process of Logging Off
        /// </summary>
        LoggingOff,

        /// <summary>
        ///     User is logged off, dispose of Session
        /// </summary>
        LoggedOff
    }
}