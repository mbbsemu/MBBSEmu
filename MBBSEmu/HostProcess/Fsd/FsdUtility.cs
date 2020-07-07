using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.CompilerServices;

namespace MBBSEmu.HostProcess.Fsd
{
    /// <summary>
    ///     This class contains helper functions related to the FSD Functionality
    /// </summary>
    public class FsdUtility
    {
        /// <summary>
        ///     Parses a Field Spec String for FSD and returns a strongly typed list of the Field Specifications
        /// </summary>
        /// <param name="fieldSpec"></param>
        /// <returns></returns>
        public List<FsdFieldSpec> ParseFieldSpecs(ReadOnlySpan<byte> fieldSpec)
        {
            if (fieldSpec == null || fieldSpec.Length == 0)
                throw new Exception("Invalid Field Spec: Cannot be empty/null");

            var result = new List<FsdFieldSpec>();

            //i will track our position within the fieldspec string
            for (var i = 0; i < fieldSpec.Length; i++)
            {
                var newSpec = new FsdFieldSpec() { FsdFieldType = EnumFsdFieldType.Text };

                //Ignore any leading spaces
                if(fieldSpec[i] == ' ')
                    continue;

                //Extract Name
                for (var n = i; n < fieldSpec.Length; n++)
                {
                    if (char.IsLetterOrDigit((char)fieldSpec[n]) || (char)fieldSpec[n] == '_')
                        continue;

                    newSpec.Name = Encoding.ASCII.GetString(fieldSpec.Slice(i, n - i));

                    i = n;
                    break;
                }

                //Skip spaces between name and beginning of spec
                while(fieldSpec[i] == ' ')
                    i++;

                //Extract Field Options (if any)
                if (fieldSpec[i] == '(')
                {
                    i++;
                    for (var o = i; o < fieldSpec.Length; o++)
                    {
                        if (fieldSpec[o] != ' ' && fieldSpec[o] != ')')
                            continue;

                        var fieldOption = Encoding.ASCII.GetString(fieldSpec.Slice(i, o - i)).ToUpper().Replace(",", string.Empty).Split('=');

                        switch (fieldOption[0])
                        {
                            case "ALT":
                                newSpec.Values.Add(fieldOption[1]);
                                break;
                            case "MIN":
                                newSpec.Minimum = int.Parse(fieldOption[1]);
                                break;
                            case "MAX":
                                newSpec.Maximum = int.Parse(fieldOption[1]);
                                break;
                            case "SECRET":
                                newSpec.FsdFieldType = EnumFsdFieldType.Secret;
                                break;
                            case "MULTICHOICE":
                                newSpec.FsdFieldType = EnumFsdFieldType.MultipleChoice;
                                break;
                        }

                        if (fieldSpec[o] == ')')
                        {
                            i = o;
                            break;
                        }

                        if (fieldSpec[o] == ' ')
                        {
                            o++; //skip the space, update option pointer
                            i = o;
                        }
                    }

                }
                else
                {
                    //The character we're most likely on is the 1st character of the next variable,
                    //so we increment back one
                    i--;

                    //Also if there's no (), it's a readonly field
                    newSpec.IsReadOnly = true;
                }

                //If no Max is specified, use the field length as max for text fields
                if (newSpec.FsdFieldType == EnumFsdFieldType.Text && newSpec.Maximum == 0 && newSpec.FieldLength > 0)
                    newSpec.Maximum = newSpec.FieldLength;

                result.Add(newSpec);
            }

            return result;
        }

        /// <summary>
        ///     Determines the X,Y coordinates of specified template fields by stripping ANSI from the field template
        ///     and parsing each character, each new line increments Y and sets X back to 1.
        ///
        ///     Note: X,Y for ANSI starts at 1,1 being the top left of the terminal (not 0,0)
        /// </summary>
        /// <param name="template"></param>
        /// <param name="status"></param>
        public void GetFieldPositions(ReadOnlySpan<byte> template, FsdStatus status)
        {
            //Get Template and Strip out ANSI Characters
            var templateString = Encoding.ASCII.GetString(StripAnsi(template));
            

            var currentY = 1; //ANSI x/y positions are 1 based, so first loop these will be 1,1
            var currentX = 0;
            var currentField = 0;
            var currentFieldLength = 0;

            var foundFieldCharacter = '\xFF';

            for (var i = 0; i < templateString.Length; i++)
            {
                var c = templateString[i];

                //Increment the X Position
                currentX++;

                //If it's the character we previously found, keep going
                if (c == foundFieldCharacter)
                {
                    currentFieldLength++;
                    continue;
                }

                //If we're past it, and the previous field Character wasn't the default, mark the length
                if (foundFieldCharacter != '\xFF')
                {
                    //Special Case for Error Field
                    if (foundFieldCharacter == '!')
                    {
                        status.ErrorField.FieldLength = currentFieldLength + 1;

                    }
                    else
                    {
                        //Standard Fields
                        status.Fields[currentField - 1].FieldLength = currentFieldLength + 1;
                    }
                    
                    currentFieldLength = 0;
                }

                //We're past the field now, reset the character
                foundFieldCharacter = '\xFF';

                //If it's a new line, increment Y and set X back to -1
                if (c == '\r')
                {
                    currentY++;
                    currentX = 0;
                    continue;
                }

                if (c != '?' && c != '$' && c != '!')
                    continue;

                //If the next character is a known control character, then we're in a field definition
                switch (templateString[i + 1])
                {
                    case '?':
                    case '$':
                    case '!':
                        break;
                    default:
                        //Otherwise keep searching
                        continue;
                }

                //Define the field based on the field specification character used
                switch (c)
                {
                    case '!': //Error Message Field
                        //Add a new field for the Error, as it's not included in the Field Spec
                        status.ErrorField = new FsdFieldSpec { FsdFieldType = EnumFsdFieldType.Error, X = currentX, Y = currentY };
                        foundFieldCharacter = c;
                        continue;
                    case '$': //Numeric Field
                        status.Fields[currentField].FsdFieldType = EnumFsdFieldType.Numeric;
                        status.Fields[currentField].X = currentX;
                        status.Fields[currentField].Y = currentY;
                        break;
                    default: //Text or Multiple Choice
                        status.Fields[currentField].X = currentX;
                        status.Fields[currentField].Y = currentY;
                        break;
                }

                currentField++;
                foundFieldCharacter = c;
            }
        }

        /// <summary>
        ///     Takes a Specified List of Answers and applies them to the corresponding Field Specs
        /// </summary>
        /// <param name="answers"></param>
        /// <param name="fields"></param>
        public void SetAnswers(List<string> answers, List<FsdFieldSpec> fields)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (answers[i].IndexOf("=", StringComparison.Ordinal) == -1)
                    continue;

                var answerComponents = answers[i].Split('=');

                fields.First(x => x.Name == answerComponents[0]).Value = answerComponents[1];
            }
        }

        /// <summary>
        ///     Parses through the template looking for leading ANSI formatting on field specifications
        /// </summary>
        /// <param name="template"></param>
        /// <param name="status"></param>
        public void GetFieldAnsi(ReadOnlySpan<byte> template, FsdStatus status)
        {
            var currentField = 0;
            var foundFieldCharacter = 0xFF;

            for (var i = 0; i < template.Length; i++)
            {
                var c = template[i];

                //If it's the character we previously found, keep going
                if (c == foundFieldCharacter)
                    continue;

                //We're past the field now, reset the character
                foundFieldCharacter = '\xFF';

                //If it's a new line, increment Y and set X back to -1
                if (c == '\r')
                    continue;

                //If we're not in a field definition, keep moving
                if (c != '?' && c != '$' && c != '!')
                    continue;

                //Found a field definition character, check to see if the next character is the same
                if (template[i + 1] != c)
                    continue;

                //Assume the ANSI format specification is within the first 10 characters
                var extractedAnsi = ExtractFieldAnsi(template.Slice(i - 10, 10));

                //Error Fields are their own special little snowflakes
                if (c == '!')
                {
                    status.ErrorField.FieldAnsi = extractedAnsi;
                }
                else
                {
                    status.Fields[currentField].FieldAnsi = extractedAnsi;
                    currentField++;
                }

                //Set our Position
                foundFieldCharacter = c;
            }
        }

        /// <summary>
        ///     Extracts the ANSI formatting from the bytes preceding the field
        ///
        ///     Assumption is bytes immediately preceding field contains ANSI formatting
        /// </summary>
        /// <param name="fieldBytes"></param>
        /// <returns></returns>
        public byte[] ExtractFieldAnsi(ReadOnlySpan<byte> fieldBytes)
        {
            //Loop backwards until we find the ANSI control character and grab everything after it
            for (var i = fieldBytes.Length - 1; i > 0; i--)
            {
                if (fieldBytes[i] == 0x1B)
                    return fieldBytes.Slice(i, fieldBytes.Length - i).ToArray();
            }

            return null;
        }

        /// <summary>
        ///     Takes a memory block of null terminated strings in a double null terminated list and loads them
        ///     into an array.
        /// </summary>
        /// <param name="answerCount"></param>
        /// <param name="answerList"></param>
        /// <returns></returns>
        public List<string> ParseAnswers(int answerCount, ReadOnlySpan<byte> answerList)
        {
            var result = new List<string>();
            using var msAnswerBuffer = new MemoryStream();

            foreach (var c in answerList)
            {
                if (c > 0)
                {
                    msAnswerBuffer.WriteByte(c);
                }
                else
                {
                    result.Add(Encoding.ASCII.GetString(msAnswerBuffer.ToArray()));
                    msAnswerBuffer.SetLength(0);
                }

                if (result.Count == answerCount)
                    return result;
            }

            throw new Exception("Unable to parse FSD Answers. Found fewer Answers than the number of Answers Specified");
        }

        /// <summary>
        ///     Strips ANSI Sequences as well as any character with an ASCII code > 127
        /// </summary>
        /// <param name="inputBuffer"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> StripAnsi(ReadOnlySpan<byte> inputBuffer)
        {
            using var msResult = new MemoryStream();

            //Replace Extended ASCII with spaces
            foreach (var c in inputBuffer)
                msResult.WriteByte(c < 127 ? c : (byte)0x20);

            var templateWithoutAnsi = new Regex(@"\x1b\[[0-9;]*m").Replace(Encoding.ASCII.GetString(msResult.ToArray()), string.Empty);

            return Encoding.ASCII.GetBytes(templateWithoutAnsi);
        }

        /// <summary>
        ///     Builds an Answer String from the Specified FsdStatus Object
        /// </summary>
        /// <param name="fsdStatus"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> BuildAnswerString(FsdStatus fsdStatus)
        {
            using var result = new MemoryStream();
            foreach (var field in fsdStatus.Fields)
            {
                result.Write(Encoding.ASCII.GetBytes($"{field.Name}={field.Value}\0"));
            }
            result.WriteByte(0);
            return result.ToArray();
        }
    }
}