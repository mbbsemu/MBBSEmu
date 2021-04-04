using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using MBBSEmu.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    /// <summary>
    ///     Sysop Global Command Handler
    /// </summary>
    public class SysopGlobal : IGlobalRoutine
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountKeyRepository _accountKeyRepository;
        private readonly IGlobalCache _globalCache;
        private readonly IMessagingCenter _messagingCenter;
        private PointerDictionary<SessionBase> _sessions;
        private Dictionary<string, MbbsModule> _modules;
        private ushort _channelNumber;

        public SysopGlobal(IAccountRepository accountRepository, IAccountKeyRepository accountKeyRepository, IGlobalCache globalCache, IMessagingCenter messagingCenter)
        {
            _accountRepository = accountRepository;
            _accountKeyRepository = accountKeyRepository;
            _globalCache = globalCache;
            _messagingCenter = messagingCenter;
        }

        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            //Fast Return
            if (command.Length < 5)
                return false;

            //Verify it's a /SYS command
            if (!Encoding.ASCII.GetString(command).ToUpper().StartsWith("/SYS") || Encoding.ASCII.GetString(command).ToUpper().StartsWith("/SYSO"))
                return false;

            //Verify the user has SYSOP key
            if (_accountKeyRepository.GetAccountKeysByUsername(sessions[channelNumber].Username)
                .Count(x => x.accountKey == "SYSOP") == 0)
                return false;

            //Set Class Variables
            _sessions = sessions;
            _channelNumber = channelNumber;
            _modules = modules;

            //Verify the command has at least one action
            if (command.IndexOf((byte)' ') == -1)
            {
                Help();
                return true;
            }

            var commandSequence = Encoding.ASCII.GetString(command).TrimEnd('\0').Split(' ');

            switch (commandSequence[1].ToUpper())
            {
                case "LISTACCOUNTS":
                    {
                        ListAccounts();
                        break;
                    }
                case "REMOVEACCOUNT":
                    {
                        RemoveAccount(commandSequence);
                        break;
                    }
                case "RESETPW":
                    {
                        ResetPassword(commandSequence);
                        break;
                    }
                case "ADDKEY":
                    {
                        AddKey(commandSequence);
                        break;
                    }
                case "REMOVEKEY":
                    {
                        RemoveKey(commandSequence);
                        break;
                    }
                case "LISTKEYS":
                    {
                        ListKeys(commandSequence);
                        break;
                    }
                case "CLEANUP":
                    {
                        Cleanup();
                        break;
                    }
                case "BROADCAST":
                    {
                        Broadcast(commandSequence);
                        break;
                    }
                case "KICK":
                    {
                        Kick(commandSequence);
                        break;
                    }
                case "HELP":
                    {
                        Help();
                        break;
                    }
                case "VERSION":
                    {
                        Version();
                        break;
                    }
                case "ENABLE":
                    {
                        ModuleEnable(commandSequence);
                        break;
                    }
                case "DISABLE":
                    {
                        ModuleDisable(commandSequence);
                        break;
                    }
                case "LISTMODULES":
                    {
                        ListModules();
                        break;
                    }
                default:
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Verifies the User specified exists on the system
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        private bool IsValidUser(string userName)
        {
            //Verify the Account Exists
            if (_accountRepository.GetAccountByUsername(userName) != null) return true;

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Invalid User: {userName}|RESET|\r\n".EncodeToANSIString());
            return false;
        }

        /// <summary>
        ///     Displays Help Text
        /// </summary>
        private void Help()
        {
            _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Sysop Commands:\r\n------------------------------------------------------------\r\n".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"LISTACCOUNTS",-30} List all accounts".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"REMOVEACCOUNT <USER>",-30} Removes account".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"RESETPW <USER> <PW> <CONF PW>",-30} Resets password for an account".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"ADDKEY <USER> <KEY>",-30} Adds a Key to a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"REMOVEKEY <USER> <KEY>",-30} Removes a Key from a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"LISTKEYS <USER>",-30} Lists Keys for a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"BROADCAST <MESSAGE>",-30} Broadcasts message to all users online".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"KICK <USER>",-30} Kick user".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"LISTMODULES",-30} Disables specified module".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"ENABLE <MODULEID>",-30} Enables specified module".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"DISABLE <MODULEID>",-30} Disables specified module".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"CLEANUP",-30} Runs Nightly Cleanup".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"VERSION",-30} Displays MBBSEmu Version\r\n".EncodeToANSIString());
            
        }

        /// <summary>
        ///     Displays MBBSEmu Version
        /// </summary>
        private void Version()
        {
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Version: |RESET||RED||B|{ new ResourceManager().GetString("MBBSEmu.Assets.version.txt") }".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to list all accounts
        ///
        ///     Syntax: /SYS LISTACCOUNTS
        /// </summary>
        private void ListAccounts()
        {
            _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Username-------------------------Email------------------------------Create Date-\r\n".EncodeToANSIString());

            foreach (var a in _accountRepository.GetAccounts())
            {
                _sessions[_channelNumber].SendToClient($"{a.userName,-33}{a.email,-35}{a.createDate.ToShortDateString()}\r\n");
            }

            _sessions[_channelNumber].SendToClient("--------------------------------------------------------------------------------\r\n|RESET|".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to delete an account
        ///
        ///     Syntax: /SYS REMOVEACCOUNT USER
        /// </summary>
        /// <param name="commandSequence"></param>
        private void RemoveAccount(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS REMOVEACCOUNT <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            
            //Verify the Account Exists
            if (!IsValidUser(userName))
                return;

            if (_sessions.Values.Any(s => string.Equals(s.Username, userName, StringComparison.CurrentCultureIgnoreCase)))
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Cannot remove logged in user: {userName}|RESET|\r\n".EncodeToANSIString());
                return;
            }
            
            //Remove the User from MBBSEmu User Database
            var userAccount = _accountRepository.GetAccountByUsername(userName);
            _accountRepository.DeleteAccountById(userAccount.accountId);

            //Remove the User from the BBSUSR Database
            var accountBtrieve = _globalCache.Get<BtrieveFileProcessor>("ACCBB-PROCESSOR");
            var result = accountBtrieve.PerformOperation(0, Encoding.ASCII.GetBytes(userAccount.userName),EnumBtrieveOperationCodes.AcquireEqual);

            if (result)
                accountBtrieve.Delete();

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Removed account: {userName}|RESET|\r\n".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to reset the password for an account
        ///
        ///     Syntax: /SYS RESETPW USER PASSWORD PASSWORD
        /// </summary>
        /// <param name="commandSequence"></param>
        private void ResetPassword(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 5)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS RESETPW <USER> <PW> <CONF PW>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            var password1 = commandSequence[3];
            var password2 = commandSequence[4];

            //Verify the Account Exists
            if (!IsValidUser(userName))
                return;

            if (password1 != password2)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Passwords do not match, please try again|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userAccount = _accountRepository.GetAccountByUsername(userName);

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Reset Password for account: {userName}|RESET|\r\n".EncodeToANSIString());
            _accountRepository.UpdateAccountById(userAccount.accountId, userAccount.userName, password1, userAccount.email);
        }

        /// <summary>
        ///     Sysop Command to add the specified key to the specified user
        ///
        ///     Syntax: /SYS ADDKEY USER KEY
        /// </summary>
        /// <param name="commandSequence"></param>
        private void AddKey(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 4)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS ADDKEY <USER> <KEY>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            var key = commandSequence[3].ToUpper();

            //Verify the Account Exists
            if (!IsValidUser(userName))
                return;

            //Verify the account doesn't already have the key
            if (_accountKeyRepository.GetAccountKeysByUsername(userName).Any(x => x.accountKey == key))
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|User {userName} already has Key {key}|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _accountKeyRepository.InsertAccountKeyByUsername(userName, key);

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Added Key {key} to User {userName}|RESET|\r\n".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to list the keys assigned to the specified user
        ///
        ///     Syntax: /SYS LISTKEYS USER
        /// </summary>
        /// <param name="commandSequence"></param>
        private void ListKeys(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS LISTKEYS <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];

            //Verify the Account Exists
            if (!IsValidUser(userName))
                return;

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Account Keys for {userName}:\r\n--------------------\r\n".EncodeToANSIString());

            foreach (var k in _accountKeyRepository.GetAccountKeysByUsername(userName))
            {
                _sessions[_channelNumber].SendToClient($"{k.accountKey}\r\n");
            }

            _sessions[_channelNumber].SendToClient($"--------------------\r\n|RESET|".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to remove the specified key from the specified user
        ///
        ///     Syntax: /SYS REMOVEKEY USER KEY
        /// </summary>
        /// <param name="commandSequence"></param>
        private void RemoveKey(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 4)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS REMOVEKEY <USER> <KEY>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            var key = commandSequence[3].ToUpper();

            //Verify the Account Exists
            if (!IsValidUser(userName))
                return;

            //Verify the account doesn't already have the key
            if (_accountKeyRepository.GetAccountKeysByUsername(userName).All(x => x.accountKey != key))
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|User {userName} doesn't have Key {key}|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _accountKeyRepository.DeleteAccountKeyByUsernameAndAccountKey(userName, key);

            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|Removed Key {key} from User {userName}|RESET|\r\n".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to broadcast a message to all users
        ///
        ///     Syntax: /SYS BROADCAST MESSAGE
        /// </summary>
        /// <param name="commandSequence"></param>
        private void Broadcast(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS BROADCAST <MESSAGE>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var message = string.Join(" ", commandSequence.Skip(2));
            
            foreach (var c in _sessions.Where(c => c.Value.Channel != _channelNumber))
                _sessions[c.Value.Channel].SendToClient($"|RESET|\r\n|B||RED|SYSOP BROADCAST: {message}|RESET|\r\n".EncodeToANSIArray());
        }

        /// <summary>
        ///     Sysop Command to kick the specified user
        ///
        ///     Syntax: /SYS KICK USER
        /// </summary>
        /// <param name="commandSequence"></param>
        private void Kick(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS KICK <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            var channelToKick = _sessions.Values.FirstOrDefault(u => u.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase));

            if (channelToKick == null)
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{userName} not found online -- Syntax: /SYS KICK <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _sessions[channelToKick.Channel].SendToClient("\r\n|RESET||RED||B|SYSOP HAS LOGGED YOU OFF\r\n|RESET|".EncodeToANSIString());
            _sessions[channelToKick.Channel].SessionState = EnumSessionState.LoggingOffDisplay;
        }

        /// <summary>
        ///     Sysop Command to enable a disabled module from the module configuration (Enabled: 0)
        ///
        ///     Syntax: /SYS ENABLE MODULEID
        /// </summary>
        /// <param name="commandSequence"></param>
        private void ModuleEnable(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS ENABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var moduleChange = commandSequence[2].ToUpper();

            if (!_modules.ContainsKey(moduleChange))
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Module -- Syntax: /SYS ENABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            if (_modules[moduleChange].ModuleConfig.ModuleEnabled)
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{_modules[moduleChange].ModuleIdentifier} already enabled -- Syntax: /SYS ENABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _messagingCenter.Send(this, EnumMessageEvent.EnableModule, _modules[moduleChange].ModuleIdentifier);
        }

        /// <summary>
        ///     Sysop Command to enable a disabled module from the module configuration (Enabled: 0)
        ///
        ///     Syntax: /SYS DISABLE MODULEID
        /// </summary>
        /// <param name="commandSequence"></param>
        private void ModuleDisable(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYS DISABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var moduleChange = commandSequence[2].ToUpper();

            if (!_modules.ContainsKey(moduleChange))
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Module -- Syntax: /SYS DISABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            if (_modules[moduleChange].ModuleConfig.ModuleEnabled == false)
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{_modules[commandSequence[2]].ModuleIdentifier} already disabled -- Syntax: /SYS DISABLE <MODULEID>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _messagingCenter.Send(this, EnumMessageEvent.DisableModule, _modules[moduleChange].ModuleIdentifier);
        }

        /// <summary>
        ///     Sysop Command to list all modules (enabled and disabled)
        ///
        ///     Syntax: /SYS LISTMODULES
        /// </summary>
        private void ListModules()
        {
            _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Module Name----------------------Path-------------------------------Enabled-----\r\n".EncodeToANSIString());

            foreach (var m in _modules)
            {
                _sessions[_channelNumber].SendToClient($"{m.Value.ModuleIdentifier,-33}{m.Value.ModulePath,-35}{m.Value.ModuleConfig.ModuleEnabled}\r\n");
            }

            _sessions[_channelNumber].SendToClient("--------------------------------------------------------------------------------\r\n|RESET|".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to manually run nightly cleanup
        ///
        ///     Syntax: /SYS CLEANUP
        /// </summary>
        private void Cleanup()
        {
            _messagingCenter.Send(this, EnumMessageEvent.Cleanup);
        }
    }
}
