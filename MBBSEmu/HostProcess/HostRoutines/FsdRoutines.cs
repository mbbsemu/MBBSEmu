using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MBBSEmu.HostProcess.HostRoutines
{
    /// <summary>
    ///     FSD/FSE Routines for MBBSEmu
    ///
    ///     Handles all Full Screen Display/Full Screen Data Entry methods as this functionality yields control
    ///     back to the host process before being passed back into the module.
    /// </summary>
    public class FsdRoutines : IHostRoutine
    {
        private readonly ILogger _logger;
        private readonly IGlobalCache _globalCache;

        private readonly Dictionary<int, FsdStatus> _fsdFields = new();
        private readonly FsdUtility _fsdUtility = new();

        private ushort _userInput;

        public FsdRoutines(ILogger logger, IGlobalCache globalCache)
        {
            _logger = logger;
            _globalCache = globalCache;
        }

        public bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            switch (session.SessionState)
            {
                case EnumSessionState.EnteringFullScreenEditor:
                case EnumSessionState.EnteringFullScreenDisplay:
                    {
                        SetState(session);
                        EnteringFullScreenDisplay(session);
                        UpdateSession(session);
                        break;
                    }
                case EnumSessionState.InFullScreenEditor:
                case EnumSessionState.InFullScreenDisplay:
                    {
                        SetState(session);
                        InFullScreenDisplay(session);
                        UpdateSession(session);
                        break;
                    }
                case EnumSessionState.ExitingFullScreenEditor:
                case EnumSessionState.ExitingFullScreenDisplay:
                    {
                        SetState(session);
                        ExitingFullScreenDisplay(session);
                        UpdateSession(session);
                        break;
                    }
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        ///     Sets any Memory States Required to process this Channel Event
        /// </summary>
        /// <param name="session"></param>
        private void SetState(SessionBase session)
        {
            //Set VDA for current Session
            var vdaChannelPointer = session.CurrentModule.Memory.GetOrAllocateVariablePointer($"VDA-{session.Channel}", Majorbbs.VOLATILE_DATA_SIZE);
            session.CurrentModule.Memory.SetArray(vdaChannelPointer, session.VDA);
            session.CurrentModule.Memory.SetArray(session.CurrentModule.Memory.GetVariablePointer("VDAPTR"), vdaChannelPointer.Data);
        }

        /// <summary>
        ///     Updates the Session with any information modified during execution that needs to persist
        /// </summary>
        /// <param name="session"></param>
        private void UpdateSession(SessionBase session)
        {
            //We Verify it exists as Unit Tests won't call SetState() which would establish the VDA for that Channel
            if (session.CurrentModule.Memory.TryGetVariablePointer($"VDA-{session.Channel}", out var vdaPointer))
                session.VDA = session.CurrentModule.Memory.GetArray(vdaPointer, Majorbbs.VOLATILE_DATA_SIZE).ToArray();
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
        ///     Gets the FSDSCB Struct for the Current Channel
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        private FsdscbStruct GetFsdscbStruct(SessionBase session)
        {
            //Get fsdscb Struct for this channel
            var fsdscbPointer = session.CurrentModule.Memory.GetVariablePointer($"FSD-Fsdscb-{session.Channel}");
            return new FsdscbStruct(session.CurrentModule.Memory.GetArray(fsdscbPointer, FsdscbStruct.Size));
        }

        /// <summary>
        ///     Saves the specified FSDSCB Struct for the Current Channel
        /// </summary>
        /// <param name="session"></param>
        /// <param name="fsdscbStruct"></param>
        private void SaveFsdscbStruct(SessionBase session, FsdscbStruct fsdscbStruct)
        {
            var fsdscbPointer = session.CurrentModule.Memory.GetVariablePointer($"FSD-Fsdscb-{session.Channel}");
            session.CurrentModule.Memory.SetArray(fsdscbPointer, fsdscbStruct.Data);
        }

        /// <summary>
        ///     Invokes the Field Verification Routine if one is specified by fsdego() in the Fsdscb Struct
        /// </summary>
        /// <param name="session"></param>
        /// <param name="fieldStatus"></param>
        /// <returns></returns>
        private bool FieldVerificationRoutine(SessionBase session, FsdStatus fieldStatus)
        {
            //Get fsdscb Struct for this channel
            var fsdscbStruct = GetFsdscbStruct(session);

            //If null, then return that it looks A-OK
            if (fsdscbStruct.fldvfy == FarPtr.Empty)
                return true;

            //Save Current Answer -- if not already allocated, allocate 255 bytes
            if (!session.CurrentModule.Memory.TryGetVariablePointer($"FSD-CurrentAnswer-{session.Channel}", out var currentAnswerPointer))
                currentAnswerPointer =
                    session.CurrentModule.Memory.AllocateVariable($"FSD-CurrentAnswer-{session.Channel}", 0xFF);
            session.CurrentModule.Memory.SetZero(currentAnswerPointer, 0xFF);
            session.CurrentModule.Memory.SetArray(currentAnswerPointer, Encoding.ASCII.GetBytes(fieldStatus.SelectedField.Value));
            session.CurrentModule.Memory.SetArray(fsdscbStruct.newans, _fsdUtility.BuildAnswerString(fieldStatus));

            //Set fsdscb struct values and save it
            fsdscbStruct.ansbuf = Encoding.ASCII.GetBytes(fieldStatus.SelectedField.Value);
            fsdscbStruct.anslen = (byte)fieldStatus.SelectedField.FieldLength;
            fsdscbStruct.crsfld = (byte)fieldStatus.SelectedOrdinal;
            fsdscbStruct.xitkey = _userInput;
            SaveFsdscbStruct(session, fsdscbStruct);

            var result = session.CurrentModule.Execute(fsdscbStruct.fldvfy, session.Channel, true, false,
                new Queue<ushort>(new List<ushort> { currentAnswerPointer.Segment, currentAnswerPointer.Offset, (ushort)fieldStatus.SelectedOrdinal }),
                0xF200); //2k from stack base of 0xFFFF, should be enough to not overlap with the existing program execution

            //Get Latest Struct
            fsdscbStruct = GetFsdscbStruct(session);

            switch (fsdscbStruct.state)
            {
                case (byte)EnumFsdStateCodes.FSDQIT:
                case (byte)EnumFsdStateCodes.FSDSAV:
                    {
                        session.SessionState = EnumSessionState.ExitingFullScreenDisplay;
                        return true;
                    }
            }

            return result.AX == (ushort)EnumFsdStateCodes.VFYCHK;
        }

        /// <summary>
        ///     If there was an error message set, this routine returns the error message to be displayed
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        private string GetVerificationErrorMessage(SessionBase session)
        {
            var message = session.CurrentModule.Memory.GetString("FSDEMG");
            return Encoding.ASCII.GetString(message);
        }

        /// <summary>
        ///     Verifies a specified field value passes the specified validation
        /// </summary>
        /// <param name="session"></param>
        /// <param name="fsdStatus"></param>
        private bool ValidateField(SessionBase session, FsdStatus fsdStatus)
        {
            var isValid = true;

            //If a field verification was passed in, we'll check it
            if (!FieldVerificationRoutine(session, fsdStatus))
            {
                DisplayErrorMessage(session, fsdStatus.ErrorField, GetVerificationErrorMessage(session));
                isValid = false;
            }

            switch (fsdStatus.SelectedField.FsdFieldType)
            {
                //Text Validations
                case EnumFsdFieldType.Text:
                case EnumFsdFieldType.Secret:
                    {
                        if (fsdStatus.SelectedField.Value?.Length > fsdStatus.SelectedField.Maximum)
                        {
                            DisplayErrorMessage(session, fsdStatus.ErrorField, $"Enter at most {fsdStatus.SelectedField.Maximum} character(s)");
                            isValid = false;
                            break;
                        }

                        if (fsdStatus.SelectedField.Value?.Length < fsdStatus.SelectedField.Minimum)
                        {
                            DisplayErrorMessage(session, fsdStatus.ErrorField, $"Enter at least {fsdStatus.SelectedField.Minimum} character(s)");
                            isValid = false;
                            break;
                        }
                        break;
                    }
                //Numeric Validation
                case EnumFsdFieldType.Numeric when !int.TryParse(fsdStatus.SelectedField.Value, out var _):
                    DisplayErrorMessage(session, fsdStatus.ErrorField, $"You must enter a value");
                    isValid = false;
                    break;
                case EnumFsdFieldType.Numeric when int.Parse(fsdStatus.SelectedField.Value) > fsdStatus.SelectedField.Maximum:
                    DisplayErrorMessage(session, fsdStatus.ErrorField, $"Enter no higher than {fsdStatus.SelectedField.Maximum}");
                    isValid = false;
                    break;
                case EnumFsdFieldType.Numeric when int.Parse(fsdStatus.SelectedField.Value) < fsdStatus.SelectedField.Minimum:
                    DisplayErrorMessage(session, fsdStatus.ErrorField, $"Enter at least {fsdStatus.SelectedField.Maximum}");
                    isValid = false;
                    break;

                //Multiple Choice Validation
                case EnumFsdFieldType.MultipleChoice when fsdStatus.SelectedField.Value == string.Empty:
                    DisplayErrorMessage(session, fsdStatus.ErrorField, $"Choices: {string.Join(' ', fsdStatus.SelectedField.Values).ToUpper()}");
                    isValid = false;
                    break;
            }

            //If Validation Failed, display error message
            if (!isValid)
            {
                //Reset the Field
                _fsdFields[session.Channel].SelectedField.Value =
                    _fsdFields[session.Channel].SelectedField.OriginalValue;

                //Mark it Active
                SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
            }

            return isValid;
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
            if (errorField == null)
                return;

            session.SendToClient($"\x1B[0;1m");

            SetCursorPosition(session, errorField.X, errorField.Y);

            if (errorField.FieldAnsi != null)
                session.SendToClient(errorField.FieldAnsi);

            session.SendToClient(new string(' ', errorField.FieldLength));
        }

        private void EnteringFullScreenDisplay(SessionBase session)
        {
            //Grab Values from Memory
            var fieldSpec = session.CurrentModule.Memory.GetString($"FSD-FieldSpec-{session.Channel}", true);
            var fsdTemplate = session.CurrentModule.Memory.GetString($"FSD-TemplateBuffer-{session.Channel}");
            var answerPointer = session.CurrentModule.Memory.GetVariablePointer($"FSD-Answers-{session.Channel}");

            //Load Fields & Answers from Spec & Description
            var fields = _fsdUtility.ParseFieldSpecs(fieldSpec);
            var parsedAnswers = _fsdUtility.ParseAnswers(fields.Count, session.CurrentModule.Memory.GetArray(answerPointer, 0x800));
            _fsdUtility.SetAnswers(parsedAnswers, fields);

            //Parse Template to know location, type, etc. of Fields
            var fsdStatus = new FsdStatus { Channel = session.Channel, SelectedOrdinal = 0, Fields = fields };
            _fsdUtility.GetFieldPositions(fsdTemplate, fsdStatus);
            _fsdUtility.GetFieldAnsi(fsdTemplate, fsdStatus);

            session.SessionState = session.SessionState == EnumSessionState.EnteringFullScreenDisplay
                ? EnumSessionState.InFullScreenDisplay
                : EnumSessionState.InFullScreenEditor;

            //Always reset on re-entry
            if (_fsdFields.ContainsKey(session.Channel))
                _fsdFields.Remove(session.Channel);

            _fsdFields.Add(session.Channel, fsdStatus);

            for (var i = 0; i < fields.Count; i++)
                SetFieldInactive(session, fsdStatus.Fields[i]);

            //Set Original Value & Highlight the First Field
            ClearErrorMessage(session, fsdStatus.ErrorField);
            SetFieldActive(session, fsdStatus.SelectedField);

            //Get fsdscb Struct for this channel
            var fsdscbStruct = GetFsdscbStruct(session);
            fsdscbStruct.numtpl = (ushort)fsdStatus.Fields.Count;
            fsdscbStruct.state = (byte)EnumFsdStateCodes.FSDAEN;
            fsdscbStruct.newans = answerPointer; //1k buffer for answer string
            _fsdUtility.SetFieldAttributes(session.CurrentModule.Memory.GetArray($"FSD-Fsdscb-{session.Channel}-flddat", FsdfldStruct.Size * 100), fields);
            SaveFsdscbStruct(session, fsdscbStruct);

        }

        /// <summary>
        ///     Processes data sent by the user and sets the ASCII (0-127) value, or the EnumKeyCode value for special characters
        /// </summary>
        /// <param name="session"></param>
        private ushort SetUserInput(SessionBase session)
        {
            var userInput = session.InputBuffer.ToArray();

            //Check to see if the user entered a backspace since it's not handled normally as userInput
            if (userInput.Length == 0 && session.CharacterProcessed == 0x8)
                userInput = new byte[] { 0x8 };

            switch (userInput.Length)
            {
                case 3 when userInput[0] == 0x1B && userInput[1] == '[': //ANSI Sequence
                    {
                        switch (userInput[2])
                        {
                            case (byte)'A':
                                return (ushort)EnumKeyCodes.CRSUP;
                            case (byte)'B':
                                return (ushort)EnumKeyCodes.CRSDN;
                            default:
                                session.InputBuffer.SetLength(0);
                                _logger.Warn($"Unsupported ANSI Sequence {userInput}");
                                break;
                        }

                        break;
                    }
                case 1 when userInput[0] != 0x1B: //ASCII
                    return userInput[0];
                case > 3:
                    session.InputBuffer.SetLength(0);
                    break;
            }

            return 0xFFFF;
        }

        /// <summary>
        ///     Handles Events while a User is within the Full Screen Data Entry Display
        /// </summary>
        /// <param name="session"></param>
        private void InFullScreenDisplay(SessionBase session)
        {
            //bail if there's no input data on the channel to process
            if (!session.DataToProcess) return;

            _userInput = SetUserInput(session);

            //Invalid Input Sequence
            if (_userInput == 0xFFFF)
                return;

            if (_userInput > byte.MaxValue) //ANSI Sequence
            {
                //Clear the input buffer
                session.InputBuffer.SetLength(0);

                //We don't validate "up/down" on the last field
                if (_fsdFields[session.Channel].SelectedOrdinal != _fsdFields[session.Channel].Fields.Count - 1)
                {
                    //Validate
                    if (!ValidateField(session, _fsdFields[session.Channel]))
                        return;
                }

                ClearErrorMessage(session, _fsdFields[session.Channel].ErrorField);

                //Set the current field back to unselected because I assume we're moving/changing at this point
                SetFieldInactive(session, _fsdFields[session.Channel].SelectedField);

                //Determine Command Entered
                switch ((EnumKeyCodes)_userInput)
                {
                    case EnumKeyCodes.CRSUP:
                        {
                            if (_fsdFields[session.Channel].SelectedOrdinal == 0)
                            {
                                //Cycle back to the bottom
                                _fsdFields[session.Channel].SelectedOrdinal =
                                    _fsdFields[session.Channel].Fields
                                        .Count(f => f.FsdFieldType != EnumFsdFieldType.Error) - 1;
                                break;
                            }

                            if (_fsdFields[session.Channel].SelectedField.OriginalValue !=
                                _fsdFields[session.Channel].SelectedField.Value)
                            {
                                //Increment Change Count
                                var fsdscbStruct = GetFsdscbStruct(session);
                                fsdscbStruct.chgcnt++;
                                SaveFsdscbStruct(session, fsdscbStruct);
                            }

                            _fsdFields[session.Channel].SelectedOrdinal--;

                            //Keep going until we find a non-readonly field
                            while (_fsdFields[session.Channel].SelectedField.IsReadOnly)
                                _fsdFields[session.Channel].SelectedOrdinal--;
                            break;
                        }
                    case EnumKeyCodes.CRSDN:
                        {
                            if (_fsdFields[session.Channel].SelectedOrdinal >= _fsdFields[session.Channel].Fields.Count - 1)
                            {
                                //Cycle back to the top
                                _fsdFields[session.Channel].SelectedOrdinal = 0;
                                break;
                            }

                            if (_fsdFields[session.Channel].SelectedField.OriginalValue !=
                                _fsdFields[session.Channel].SelectedField.Value)
                            {
                                //Increment Change Count
                                var fsdscbStruct = GetFsdscbStruct(session);
                                fsdscbStruct.chgcnt++;
                                SaveFsdscbStruct(session, fsdscbStruct);
                            }

                            _fsdFields[session.Channel].SelectedOrdinal++;

                            //Keep going until we find a non-readonly field
                            while (_fsdFields[session.Channel].SelectedField.IsReadOnly)
                                _fsdFields[session.Channel].SelectedOrdinal++;

                            break;
                        }
                    default:
                        return;
                }

                SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                return;
            }

            if (_userInput is >= 32 and <= 126) //ASCII Character Input
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
                            //Don't let us input more than the field length
                            if (_fsdFields[session.Channel].SelectedField.FieldLength == _fsdFields[session.Channel].SelectedField.Value?.Length)
                                break;

                            //If it's numeric, ensure what was entered was in fact a number
                            if (_fsdFields[session.Channel].SelectedField.FsdFieldType == EnumFsdFieldType.Numeric && !char.IsNumber((char)_userInput))
                                break;

                            _fsdFields[session.Channel].SelectedField.Value += (char)_userInput;

                            break;
                        }

                    //Multiple Select
                    case EnumFsdFieldType.MultipleChoice:
                        {
                            //spacebar cycles through field values
                            if (_userInput == 32)
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
                                if (!_fsdFields[session.Channel].SelectedField.Values[i]
                                    .StartsWith(((char)_userInput).ToString(),
                                        StringComparison.InvariantCultureIgnoreCase)) continue;

                                _fsdFields[session.Channel].SelectedField.SelectedValue = i;
                                _fsdFields[session.Channel].SelectedField.Value = _fsdFields[session.Channel].SelectedField.Values[_fsdFields[session.Channel].SelectedField.SelectedValue];
                                SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                                break;

                            }

                            break;
                        }
                }
            }

            if (_userInput is <= 31 or 127) //Special Characters & Delete
            {
                switch (_userInput)
                {
                    case 127: //DEL
                    case 8: //BACKSPACE
                        {
                            if (_fsdFields[session.Channel].SelectedField.Value.Length > 0)
                            {
                                _fsdFields[session.Channel].SelectedField.Value =
                                    _fsdFields[session.Channel].SelectedField.Value.Substring(0, _fsdFields[session.Channel].SelectedField.Value.Length - 1);

                                session.SendToClient(new byte[] { 0x08, 0x20, 0x08 });
                            }

                            break;
                        }

                    case 'X' - 64: //CRTL-X
                    case 'Q' - 64: //CTRL-Q
                    case 'S' - 64: //CTRL-S
                        {
                            ValidateField(session, _fsdFields[session.Channel]);
                            break;
                        }
                    case 13: //CR
                        {
                            //Clear the input buffer
                            session.InputBuffer.SetLength(0);

                            if (_fsdFields[session.Channel].SelectedOrdinal >= _fsdFields[session.Channel].Fields.Count)
                                return;

                            //Validate
                            if (!ValidateField(session, _fsdFields[session.Channel]))
                                return;

                            if (_fsdFields[session.Channel].SelectedField.OriginalValue !=
                                _fsdFields[session.Channel].SelectedField.Value)
                            {
                                //Increment Change Count
                                var fsdscbStruct = GetFsdscbStruct(session);
                                fsdscbStruct.chgcnt++;
                                SaveFsdscbStruct(session, fsdscbStruct);
                            }

                            ClearErrorMessage(session, _fsdFields[session.Channel].ErrorField);
                            SetFieldInactive(session, _fsdFields[session.Channel].SelectedField);

                            //Hitting Enter on the last Field
                            if (_fsdFields[session.Channel].SelectedOrdinal ==
                                _fsdFields[session.Channel].Fields.Count - 1)
                            {
                                //Final Field in FSE is for Savings
                                if (session.SessionState == EnumSessionState.InFullScreenEditor)
                                {
                                    var fseFinalState =
                                        session.CurrentModule.Memory.GetOrAllocateVariablePointer(
                                            $"FSE-FinalStateCode-{session.Channel}", sizeof(ushort));
                                    switch (_fsdFields[session.Channel].SelectedField.Value)
                                    {
                                        case "SAVE":
                                            session.CurrentModule.Memory.SetWord(fseFinalState, 0);
                                            break;
                                        case "QUIT":
                                            session.CurrentModule.Memory.SetWord(fseFinalState, 256);
                                            break;
                                    }

                                    session.SessionState = EnumSessionState.ExitingFullScreenEditor;
                                    return;
                                }
                            }

                            _fsdFields[session.Channel].SelectedOrdinal++;

                            //Keep going until we find a non-readonly field
                            while (_fsdFields[session.Channel].SelectedField.IsReadOnly)
                                _fsdFields[session.Channel].SelectedOrdinal++;

                            SetFieldActive(session, _fsdFields[session.Channel].SelectedField);
                            return;
                        }
                }
            }
        }

        private void ExitingFullScreenDisplay(SessionBase session)
        {
            var fsdscbStruct = GetFsdscbStruct(session);

            //Get Entry Point for "When Done" routine
            var fsdWhenDoneRoutine = session.CurrentModule.Memory.GetPointer($"FSD-WhenDoneRoutine-{session.Channel}");

            //Save FSD Status to Global Cache
            _globalCache.Set($"FSD-Status-{session.Channel}", _fsdFields[session.Channel]);

            //Build Answers String
            var answersString = string.Join('\0', _fsdFields[session.Channel].Fields.Select(x => x.Value));
            session.CurrentModule.Memory.SetZero(fsdscbStruct.newans, 0x800);
            session.CurrentModule.Memory.SetArray(fsdscbStruct.newans, Encoding.ASCII.GetBytes(answersString));

            //Reset Screen for Exit
            SetCursorPosition(session, 0, 24);
            session.SendToClient("|RESET|".EncodeToANSIArray());
            session.SendToClient("|GREEN||B|".EncodeToANSIArray());

            //Get Result Code if it was FSE or FSD
            ushort resultCode = 0;
            if (session.SessionState == EnumSessionState.ExitingFullScreenDisplay)
                resultCode = (ushort)(fsdscbStruct.state == (byte)EnumFsdStateCodes.FSDSAV ? 1 : 0);
            else if (session.SessionState == EnumSessionState.ExitingFullScreenEditor)
            {
                //Set FSE Result Code
                resultCode = session.CurrentModule.Memory.GetWord($"FSE-FinalStateCode-{session.Channel}");

                //Set FSE Text & Topic Values
                var textPointer = session.CurrentModule.Memory.GetPointer($"FSE-TextPointer-{session.Channel}");
                session.CurrentModule.Memory.SetArray(textPointer,
                    Encoding.ASCII.GetBytes($"{CompileFSEText(session)}\0"));

                var topicPointer = session.CurrentModule.Memory.GetPointer($"FSE-TopicPointer-{session.Channel}");
                session.CurrentModule.Memory.SetArray(topicPointer,
                    Encoding.ASCII.GetBytes($"{_fsdFields[session.Channel].Fields.First(x => x.Name == "TOPIC").Value}\0"));

                //If the module is using VDA, we need to ensure this data is saved to the session before invoking the "when done" method
                UpdateSession(session);
            }

            //Invoke When Done Routine
            var _ = session.CurrentModule.Execute(fsdWhenDoneRoutine, session.Channel, true, false,
                new Queue<ushort>(new List<ushort> { resultCode }),
                0xF100); //3k from stack base of 0xFFFF, should be enough to not overlap with the existing program execution

            //Invokes STT on exit
            session.SessionState = EnumSessionState.InModule;
            session.Status.Enqueue(3);
            session.UsrPtr.Substt++;
        }

        /// <summary>
        ///     Grabs the Multiple Lines from the FSD Template for the FSE and compiles it into a final response string
        /// </summary>
        /// <param name="session"></param>
        private string CompileFSEText(SessionBase session)
        {
            var result = new StringBuilder();

            foreach (var line in _fsdFields[session.Channel].Fields.Where(x => x.Name.StartsWith("LINE")))
                result.Append($"{line.Value?.TrimEnd('\0')}\r");

            //Trim off trailing blank lines
            return result.ToString().TrimEnd('\r');
        }
    }
}
