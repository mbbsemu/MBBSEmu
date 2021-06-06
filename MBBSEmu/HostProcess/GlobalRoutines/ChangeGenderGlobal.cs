using MBBSEmu.Extensions;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.GlobalRoutines
{
    public class ChangeGenderGlobal : IGlobalRoutine
    {
        public bool ProcessCommand(ReadOnlySpan<byte> command, ushort channelNumber, PointerDictionary<SessionBase> sessions, Dictionary<string, MbbsModule> modules)
        {
            var commandString = Encoding.ASCII.GetString(command).TrimEnd('\0');

            if (commandString == "/!")
            {
                sessions[channelNumber].SendToClient("|RESET|\r\n|B||GREEN|CHANGING GENDER TO: |RESET|\r\n".EncodeToANSIArray());

                //TODO Add AccountRepository to globals or move to sysop command
                
                return true;
            }

            return false;
        }
    }
}
