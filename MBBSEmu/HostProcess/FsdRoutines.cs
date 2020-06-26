using System;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;

namespace MBBSEmu.HostProcess
{
    public class FsdRoutines : IMbbsRoutines
    {
        private readonly Dictionary<int, FsdStatus> _fsdFields = new Dictionary<int, FsdStatus>();
        private readonly FsdUtility _fsdUtility = new FsdUtility();

        public bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            switch (session.SessionState)
            {
                case EnumSessionState.EnteringFullScreenDisplay:
                    EnteringFullScreenDisplay(session);
                    break;
                case EnumSessionState.InFullScreenDisplay:
                    InFullScreenDisplay(session);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void ProcessCharacter(SessionBase session, bool secure = false)
        {
            //Check to see if there's anything that needs doing
            if (!session.DataToProcess) return;

            session.DataToProcess = false;
            switch (session.LastCharacterReceived)
            {
                case 0x8:
                case 0x7F:
                    EchoToClient(session, new byte[] { 0x08, 0x20, 0x08 });
                    session.InputBuffer.SetLength(session.InputBuffer.Length - 1);
                    break;
                default:
                    EchoToClient(session, secure ? (byte)0x2A : session.LastCharacterReceived);
                    break;
            }

            HandleBackspace(session, session.LastCharacterReceived);
        }

        private void EchoToClient(SessionBase session, ReadOnlySpan<byte> dataToEcho)
        {
            foreach (var t in dataToEcho)
                EchoToClient(session, t);
        }

        /// <summary>
        ///     Data to Echo to client
        ///
        ///     This will handle cases like properly doing backspaces, etc.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dataToEcho"></param>
        private void EchoToClient(SessionBase session, byte dataToEcho)
        {
            //Handle Backspace or Delete
            if (dataToEcho == 0x8 || dataToEcho == 0x7F)
            {
                session.SendToClient(new byte[] { 0x08, 0x20, 0x08 });
                return;
            }

            session.SendToClient(new[] { dataToEcho });
        }

        /// <summary>
        ///     Applies Backspace from the Client to the Input Buffer
        /// </summary>
        /// <param name="session"></param>
        /// <param name="inputFromUser"></param>
        /// <returns></returns>
        private void HandleBackspace(SessionBase session, byte inputFromUser)
        {
            if (inputFromUser != 0x8 && inputFromUser != 0x7F)
                return;

            //If its backspace and the buffer is already empty, do nothing
            if (session.InputBuffer.Length <= 0)
                return;

            //Because position starts at 0, it's an easy -1 on length
            session.InputBuffer.SetLength(session.InputBuffer.Length - 1);
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

        private void HighlightField(SessionBase session, int fieldNumber)
        {
            var field = _fsdFields[session.Channel].Fields[fieldNumber];

            SetCursorPosition(session, field.X, field.Y);
            session.SendToClient($"\x1B[0;1;7m");
            session.SendToClient(new string(' ', field.FieldLength));
            SetCursorPosition(session, field.X, field.Y);
            session.SendToClient(field.Value);
        }

        private void ResetField(SessionBase session, int fieldNumber)
        {
            var field = _fsdFields[session.Channel].Fields[fieldNumber];

            //Reset Formatting
            session.SendToClient($"\x1B[0;1m");

            SetCursorPosition(session, field.X, field.Y);

            if (field.FieldAnsi != null)
                session.SendToClient(field.FieldAnsi);

            session.SendToClient(new string(' ', field.FieldLength));
            SetCursorPosition(session, field.X, field.Y);

            if (field.Value != null)
                session.SendToClient(field.FsdFieldType == EnumFsdFieldType.Secret
                    ? new string('*', field.FieldLength)
                    : field.Value);
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
            _fsdUtility.GetFieldAnsi(fsdTemplate, fields);

            session.SessionState = EnumSessionState.InFullScreenDisplay;
            var fsdStatus = new FsdStatus { Fields = fields, Channel = session.Channel, SelectedField = 0 };

            if (!_fsdFields.ContainsKey(session.Channel))
            {
                _fsdFields[session.Channel] = fsdStatus;
            }
            else
            {
                _fsdFields.Add(session.Channel, fsdStatus);
            }

            for (var i = 0; i < fields.Count; i++)
            {
                ResetField(session, i);
                /*
                var field = fields[i];
                SetCursorPosition(session, field.X, field.Y);
                session.SendToClient(new string(' ', field.FieldLength));
                SetCursorPosition(session, field.X, field.Y);

                if (field.FieldAnsi != null)
                    session.SendToClient(field.FieldAnsi);

                if (field.Value != null)
                    session.SendToClient(field.FsdFieldType == EnumFsdFieldType.Secret
                        ? new string('*', field.FieldLength)
                        : field.Value);

                */
            }

            //Highlight the First Field
            HighlightField(session, 0);
        }

        private void InFullScreenDisplay(SessionBase session)
        {
            if (session.DataToProcess)
            {
                if (session.InputBuffer.Length == 3)
                {
                    var inputCode = session.InputBuffer.ToArray();

                    if (inputCode[0] != 0x1B || inputCode[1] != '[')
                        return;

                    session.InputBuffer.SetLength(0);
                    ResetField(session, _fsdFields[session.Channel].SelectedField);

                    switch (inputCode[2])
                    {
                        case (byte)'A':
                            {
                                if (_fsdFields[session.Channel].SelectedField == 0)
                                    return;

                                _fsdFields[session.Channel].SelectedField--;
                                break;
                            }
                        case (byte)'B':
                            {
                                if (_fsdFields[session.Channel].SelectedField >= _fsdFields[session.Channel].Fields.Count)
                                    return;

                                _fsdFields[session.Channel].SelectedField++;
                                break;
                            }
                        default:
                            return;
                    }

                    HighlightField(session, _fsdFields[session.Channel].SelectedField);
                    return;
                }
                else
                {
                    //Not incoming ANSI
                    if (session.InputBuffer.Length > 0 && session.InputBuffer.ToArray()[0] != 0x1B)
                        ProcessCharacter(session);
                }

            }
        }
    }
}
