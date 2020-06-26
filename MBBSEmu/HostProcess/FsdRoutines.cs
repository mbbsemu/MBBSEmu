using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

        /// <summary>
        ///     Sets the Cursor Position to the Specified X & Y
        /// </summary>
        /// <param name="session"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private void SetCursorPosition(SessionBase session, int x, int y)
        {
            session.SendToClient($"\x1B[{y};{x}f");
        }

        private void EnteringFullScreenDisplay(SessionBase session)
        {
            var fieldSpec = session.CurrentModule.Memory.GetString($"FSD-FieldSpec-{session.Channel}", true);
            var fsdTemplate = session.CurrentModule.Memory.GetString($"FSD-TemplateBuffer-{session.Channel}");
            var answerPointer = session.CurrentModule.Memory.GetVariablePointer($"FSD-Answers-{session.Channel}");

            var fields = _fsdUtility.ParseFieldSpecs(fieldSpec);

            var answersLoaded = 0;
            using var msAnswerBuffer = new MemoryStream();
            var answerList = new List<string>();
            while (answersLoaded < fields.Count)
            {
                var c = session.CurrentModule.Memory.GetByte(answerPointer.Segment, answerPointer.Offset++);

                if (c > 0)
                {
                    msAnswerBuffer.WriteByte(c);
                }
                else
                {
                    answerList.Add(Encoding.ASCII.GetString(msAnswerBuffer.ToArray()));
                    msAnswerBuffer.SetLength(0);
                    answersLoaded++;
                }
            }

            _fsdUtility.SetAnswers(answerList, fields);


            _fsdUtility.GetFieldPositions(fsdTemplate, fields);
            
            foreach (var field in fields)
            {
                SetCursorPosition(session, field.X, field.Y);
                session.SendToClient(new string(' ', field.FieldLength));
                SetCursorPosition(session, field.X, field.Y);
                session.SendToClient(field.Value);
            }

        }
    }
}
