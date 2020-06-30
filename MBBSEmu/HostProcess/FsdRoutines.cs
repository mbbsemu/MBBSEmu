using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        /// <summary>
        ///     Sets the Specified Field to Active (Cursor Entered, Highlighted)
        /// </summary>
        /// <param name="session"></param>
        /// <param name="field"></param>
        private void SetFieldActive(SessionBase session, FsdFieldSpec field)
        {
            SetCursorPosition(session, field.X, field.Y);
            session.SendToClient($"\x1B[0;1;7m");
            session.SendToClient(new string(' ', field.FieldLength));
            SetCursorPosition(session, field.X, field.Y);
            session.SendToClient(field.Value ?? string.Empty);

            //Set Original Value to be reset upon validation failure
            field.OriginalValue = field.Value;
        }

        /// <summary>
        ///     Sets the specified Field to Inactive Default (Default Formatting)
        /// </summary>
        /// <param name="session"></param>
        /// <param name="field"></param>
        private void SetFieldInactive(SessionBase session, FsdFieldSpec field)
        {
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

        /// <summary>
        ///     Verifies a specified field value passes the specified validation
        /// </summary>
        /// <param name="field"></param>
        private bool ValidateField(SessionBase session, FsdFieldSpec field, FsdFieldSpec errorField)
        {
            switch (field.FsdFieldType)
            {
                //Text Validations
                case EnumFsdFieldType.Text:
                case EnumFsdFieldType.Secret:
                    {
                        if (field.Value.Length > field.Maximum)
                        {
                            DisplayErrorMessage(session, errorField, $"Enter at most {field.Maximum} character(s)");
                            return false;
                        }

                        if (field.Value.Length < field.Minimum)
                        {
                            DisplayErrorMessage(session, errorField, $"Enter at least {field.Minimum} character(s)");
                            return false;
                        }

                        break;
                    }
                //Numeric Validation
                case EnumFsdFieldType.Numeric when int.Parse(field.Value) > field.Maximum:
                    DisplayErrorMessage(session, errorField, $"Enter no higher than {field.Maximum}");
                    return false;
                case EnumFsdFieldType.Numeric when int.Parse(field.Value) < field.Minimum:
                    DisplayErrorMessage(session, errorField, $"Enter at least {field.Maximum}");
                    return false;

                //Multiple Choice Validation
                case EnumFsdFieldType.MultipleChoice when field.Value == string.Empty:
                    DisplayErrorMessage(session, errorField, $"Choices: {string.Join(' ', field.Values).ToUpper()}");
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Displays an Error Message to the User
        /// </summary>
        /// <param name="session"></param>
        /// <param name="errorField"></param>
        /// <param name="errorMessage"></param>
        private void DisplayErrorMessage(SessionBase session, FsdFieldSpec errorField, string errorMessage)
        {
            //Reset Formatting
            session.SendToClient($"\x1B[0;1m");

            SetCursorPosition(session, errorField.X, errorField.Y);

            if (errorField.FieldAnsi != null)
                session.SendToClient(errorField.FieldAnsi);

            session.SendToClient(new string(' ', errorField.FieldLength));
            SetCursorPosition(session, errorField.X, errorField.Y);

            session.SendToClient(errorMessage);
        }

        /// <summary>
        ///     Clears a Displayed Error Message to the User
        /// </summary>
        /// <param name="session"></param>
        /// <param name="errorField"></param>
        private void ClearErrorMessage(SessionBase session, FsdFieldSpec errorField)
        {
            session.SendToClient($"\x1B[0;1m");

            SetCursorPosition(session, errorField.X, errorField.Y);

            if (errorField.FieldAnsi != null)
                session.SendToClient(errorField.FieldAnsi);

            session.SendToClient(new string(' ', errorField.FieldLength));
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
            var fsdStatus = new FsdStatus { Fields = fields, Channel = session.Channel, SelectedOrdinal = 0 };

            if (!_fsdFields.ContainsKey(session.Channel))
            {
                _fsdFields[session.Channel] = fsdStatus;
            }
            else
            {
                _fsdFields.Add(session.Channel, fsdStatus);
            }

            for (var i = 0; i < fields.Count; i++)
                SetFieldInactive(session, fsdStatus.Fields[i]);

            //Set Original Value & Highlight the First Field
            SetFieldActive(session, fsdStatus.SelectedField);
        }

        private void InFullScreenDisplay(SessionBase session)
        {
            //Is there data from the user?
            if (session.DataToProcess)
            {
                var userInput = session.InputBuffer.ToArray();

                //ANSI codes are 3 byte sequences
                //Verify First 2 bytes are ANSI escape
                if (userInput.Length == 3 && userInput[0] == 0x1B && userInput[1] == '[')
                {
                    //Clear the input buffer
                    session.InputBuffer.SetLength(0);

                    //Validate
                    if (!ValidateField(session, _fsdFields[session.Channel].SelectedField, _fsdFields[session.Channel].Fields.FirstOrDefault(x => x.FsdFieldType == EnumFsdFieldType.Error)))
                    {
                        //Reset the Field
                        _fsdFields[session.Channel].SelectedField.Value =
                            _fsdFields[session.Channel].SelectedField.OriginalValue;

                        //Mark it Active
                        SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                        return;
                    }

                    ClearErrorMessage(session, _fsdFields[session.Channel].Fields.FirstOrDefault(x => x.FsdFieldType == EnumFsdFieldType.Error));

                    //Set the current field back to unselected because I assume we're moving/changing at this point
                    SetFieldInactive(session, _fsdFields[session.Channel].SelectedField);

                    //Determine Command Entered
                    switch (userInput[2])
                    {
                        case (byte)'A':
                            {
                                if (_fsdFields[session.Channel].SelectedOrdinal == 0)
                                {
                                    //Cycle back to the bottom
                                    _fsdFields[session.Channel].SelectedOrdinal =
                                        _fsdFields[session.Channel].Fields
                                            .Count(f => f.FsdFieldType != EnumFsdFieldType.Error) - 1;
                                    break;
                                }

                                _fsdFields[session.Channel].SelectedOrdinal--;
                                break;
                            }
                        case (byte)'B':
                            {
                                if (_fsdFields[session.Channel].SelectedOrdinal >= _fsdFields[session.Channel].Fields.Count(f => f.FsdFieldType != EnumFsdFieldType.Error) - 1)
                                {
                                    //Cycle back to the top
                                    _fsdFields[session.Channel].SelectedOrdinal = 0;
                                    break;
                                }

                                _fsdFields[session.Channel].SelectedOrdinal++;
                                break;
                            }
                        default:
                            return;
                    }

                    SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                    return;
                }

                //Enter Key Cycles through Fields
                if (userInput.Length == 1 && userInput[0] == '\r')
                {
                    //Clear the input buffer
                    session.InputBuffer.SetLength(0);

                    if (_fsdFields[session.Channel].SelectedOrdinal >= _fsdFields[session.Channel].Fields.Count)
                        return;

                    //Validate
                    if (!ValidateField(session, _fsdFields[session.Channel].SelectedField, _fsdFields[session.Channel].Fields.FirstOrDefault(x => x.FsdFieldType == EnumFsdFieldType.Error)))
                    {
                        //Reset the Field
                        _fsdFields[session.Channel].SelectedField.Value =
                            _fsdFields[session.Channel].SelectedField.OriginalValue;

                        //Mark it Active
                        SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                        return;
                    }
                    ClearErrorMessage(session, _fsdFields[session.Channel].Fields.FirstOrDefault(x => x.FsdFieldType == EnumFsdFieldType.Error));
                    SetFieldInactive(session, _fsdFields[session.Channel].SelectedField);
                    _fsdFields[session.Channel].SelectedOrdinal++;
                    SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                    return;
                }

                //User Typed a Character
                if (userInput.Length == 1 && userInput[0] != 0x1B)
                {
                    //Clear the input buffer
                    session.InputBuffer.SetLength(0);

                    //Not incoming ANSI
                    switch (_fsdFields[session.Channel].SelectedField.FsdFieldType)
                    {
                        //Text Box Input
                        case EnumFsdFieldType.Secret:
                        case EnumFsdFieldType.Text:
                        case EnumFsdFieldType.Numeric:
                            {
                                if (userInput[0] == 0x7F || userInput[0] == 0x8)
                                {
                                    if (_fsdFields[session.Channel].SelectedField.Value.Length > 0)
                                    {
                                        ProcessCharacter(session);
                                        _fsdFields[session.Channel].SelectedField.Value =
                                            _fsdFields[session.Channel].SelectedField.Value.Substring(0, _fsdFields[session.Channel].SelectedField.Value.Length - 1);
                                    }
                                }
                                else
                                {
                                    //Don't let us input more than the field length
                                    if (_fsdFields[session.Channel].SelectedField.FieldLength == _fsdFields[session.Channel].SelectedField.Value.Length)
                                        break;

                                    //If it's numeric, ensure what was entered was in fact a number
                                    if (_fsdFields[session.Channel].SelectedField.FsdFieldType == EnumFsdFieldType.Numeric && !char.IsNumber((char)userInput[0]))
                                        break;

                                    ProcessCharacter(session);
                                    _fsdFields[session.Channel].SelectedField.Value += (char)userInput[0];
                                }

                                break;
                            }

                        //Multiple Select
                        case EnumFsdFieldType.MultipleChoice:
                            {
                                //ENTER cycles fields
                                if (userInput[0] == 0x20) //space
                                {
                                    _fsdFields[session.Channel].SelectedField.SelectedValue++;

                                    if (_fsdFields[session.Channel].SelectedField.SelectedValue >= _fsdFields[session.Channel].SelectedField.Values.Count)
                                        _fsdFields[session.Channel].SelectedField.SelectedValue = 0;

                                    _fsdFields[session.Channel].SelectedField.Value = _fsdFields[session.Channel]
                                        .SelectedField.Values[_fsdFields[session.Channel].SelectedField.SelectedValue];
                                    SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                                    break;
                                }

                                //First Character of one of the choices selects it
                                for (var i = 0; i < _fsdFields[session.Channel].SelectedField.Values.Count; i++)
                                {
                                    if (_fsdFields[session.Channel].SelectedField.Values[i].StartsWith(((char)userInput[0]).ToString(), StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        _fsdFields[session.Channel].SelectedField.SelectedValue = i;
                                        _fsdFields[session.Channel].SelectedField.Value = _fsdFields[session.Channel].SelectedField.Values[_fsdFields[session.Channel].SelectedField.SelectedValue];
                                        SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                                        break;
                                    }

                                }

                                break;
                            }

                    }

                }

            }
        }
    }
}
