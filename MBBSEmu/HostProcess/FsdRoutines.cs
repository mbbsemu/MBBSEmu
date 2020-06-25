using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.Module;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess
{
    public class FsdRoutines : IMbbsRoutines
    {
        private readonly Dictionary<int, List<FsdFieldSpec>> _fsdFields = new Dictionary<int, List<FsdFieldSpec>>();
        private readonly FsdUtility _fsdUtility = new FsdUtility();

        public bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            switch (session.SessionState)
            {
                case EnumSessionState.EnteringFullScreenDisplay:
                    EnteringFullScreenDisplay(session);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private void EnteringFullScreenDisplay(SessionBase session)
        {
            var fieldSpec = session.CurrentModule.Memory.GetString(session.CurrentModule.Memory.GetVariablePointer($"FSD-FieldSpec-{session.Channel}"), true);

            var fields = _fsdUtility.ParseFieldSpecs(fieldSpec);

        }
    }
}
