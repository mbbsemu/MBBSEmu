using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
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
        private PointerDictionary<SessionBase> _sessions;
        private ushort _channelNumber;

        public SysopGlobal(IAccountRepository accountRepository, IAccountKeyRepository accountKeyRepository, IGlobalCache globalCache)
        {
            _accountRepository = accountRepository;
            _accountKeyRepository = accountKeyRepository;
            _globalCache = globalCache;
        }

        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            //Fast Return
            if (command.Length < 6)
                return false;

            //Verify it's a /SYSOP command
            if (!Encoding.ASCII.GetString(command).ToUpper().StartsWith("/SYSOP"))
                return false;

            //Verify the user has SYSOP key
            if (_accountKeyRepository.GetAccountKeysByUsername(sessions[channelNumber].Username)
                .Count(x => x.accountKey == "SYSOP") == 0)
                return false;

            //Set Class Variables
            _sessions = sessions;
            _channelNumber = channelNumber;

            //Verify the command has at least one action
            if (command.IndexOf((byte)' ') == -1)
            {
                Help();
                return true;
            }

            var commandSequence = Encoding.ASCII.GetString(command).TrimEnd('\0').Split(' ');

            switch (commandSequence[1].ToUpper())
            {
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
                        _globalCache.Set("SYSOPGLOBAL-CLEANUP", true);
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
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"ADDKEY <USER> <KEY>",-30} Adds a Key to a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"REMOVEKEY <USER> <KEY>",-30} Removes a Key from a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"LISTKEYS <USER>",-30} Lists Keys for a User".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"BROADCAST <MESSAGE>",-30} Broadcasts message to all users online".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"KICK <USER>",-30} Kick user".EncodeToANSIString());
            _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{"CLEANUP",-30} Runs Nightly Cleanup\r\n".EncodeToANSIString());
        }

        /// <summary>
        ///     Sysop Command to add the specified key to the specified user
        ///
        ///     Syntax: /SYSOP ADDKEY USER KEY
        /// </summary>
        /// <param name="commandSequence"></param>
        private void AddKey(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 4)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYSOP ADDKEY <USER> <KEY>|RESET|\r\n".EncodeToANSIString());
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
        ///     Syntax: /SYSOP LISTKEYS USER
        /// </summary>
        /// <param name="commandSequence"></param>
        private void ListKeys(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYSOP LISTKEYS <USER>|RESET|\r\n".EncodeToANSIString());
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

        private void RemoveKey(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 4)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYSOP REMOVEKEY <USER> <KEY>|RESET|\r\n".EncodeToANSIString());
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
        ///     Syntax: /SYSOP BROADCAST MESSAGE
        /// </summary>
        /// <param name="commandSequence"></param>
        private void Broadcast(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYSOP BROADCAST <MESSAGE>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var message = string.Join(" ", commandSequence.Skip(2));
            
            foreach (var c in _sessions)
                if (c.Value.Channel != _channelNumber)
                    _sessions[c.Value.Channel].SendToClient($"|RESET|\r\n|B||RED|SYSOP BROADCAST: {message}|RESET|\r\n".EncodeToANSIArray());
        }

        /// <summary>
        ///     Sysop Command to kick the specified user
        ///
        ///     Syntax: /SYSOP KICK USER
        /// </summary>
        /// <param name="commandSequence"></param>
        private void Kick(IReadOnlyList<string> commandSequence)
        {
            if (commandSequence.Count() < 3)
            {
                _sessions[_channelNumber].SendToClient("\r\n|RESET||WHITE||B|Invalid Command -- Syntax: /SYSOP KICK <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            var userName = commandSequence[2];
            var channelToKick = _sessions.Values.FirstOrDefault(u => u.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase));

            if (channelToKick == null)
            {
                _sessions[_channelNumber].SendToClient($"\r\n|RESET||WHITE||B|{userName} not found online -- Syntax: /SYSOP KICK <USER>|RESET|\r\n".EncodeToANSIString());
                return;
            }

            _sessions[channelToKick.Channel].SendToClient("\r\n|RESET||RED||B|SYSOP HAS LOGGED YOU OFF\r\n|RESET|".EncodeToANSIString());
            _sessions[channelToKick.Channel].SessionState = EnumSessionState.LoggingOffDisplay;
        }
    }
}
