using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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

            for (var i = 0; i < fieldSpec.Length; i++)
            {
                var newSpec = new FsdFieldSpec() { FsdFieldType = EnumFsdFieldType.Text };

                if (fieldSpec[i] == ' ')
                    continue;

                //Extract Name
                for (var n = i; n < fieldSpec.Length; n++)
                {
                    if (char.IsLetterOrDigit((char)fieldSpec[n]))
                        continue;

                    newSpec.Name = Encoding.ASCII.GetString(fieldSpec.Slice(i, n - i));

                    i = n;
                    break;
                }

                if (fieldSpec[i] == ' ')
                    i++;

                //Extract Field Options (if there)
                if (fieldSpec[i] == '(')
                {
                    i++;
                    for (var o = i; o < fieldSpec.Length; o++)
                    {
                        if (fieldSpec[o] != ' ' && fieldSpec[o] != ')')
                            continue;

                        var fieldOption = Encoding.ASCII.GetString(fieldSpec.Slice(i, o - i)).ToUpper().Split('=');

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
                }

                result.Add(newSpec);
            }

            return result;
        }

        public void GetFieldPositions(ReadOnlySpan<byte> template, List<FsdFieldSpec> fields)
        {
            //Get Template and Strip out ANSI Characters
            var templateString = Encoding.ASCII.GetString(template.ToArray());
            var templateWithoutAnsi = new Regex(@"\x1b\[[0-9;]*m").Replace(templateString, string.Empty);

            var currentY = 1; //ANSI x/y positions are 1 based, so first loop these will be 1,1
            var currentX = 0;
            var currentField = 0;
            var currentFieldLength = 0;

            var foundFieldCharacter = '\xFF';

            for (var i = 0; i < templateWithoutAnsi.Length; i++)
            {
                var c = templateWithoutAnsi[i];
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
                    fields[currentField - 1].FieldLength = currentFieldLength + 1;
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

                if (c != '?' && c != '$')
                    continue;

                if (c == '$')
                    fields[currentField].FsdFieldType = EnumFsdFieldType.Numeric;

                if (templateWithoutAnsi[i + 1] != '?' && templateWithoutAnsi[i + 1] != '$')
                    continue;

                //Set our Position
                fields[currentField].X = currentX;
                fields[currentField].Y = currentY;
                currentField++;
                foundFieldCharacter = c;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="answers"></param>
        /// <param name="fields"></param>
        public void SetAnswers(List<string> answers, List<FsdFieldSpec> fields)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                //Sometimes the "DONE" or last item doesn't have an answer specified
                if (answers[i].IndexOf("=", StringComparison.Ordinal) == -1)
                    continue;

                fields[i].Value = answers[i].Substring(answers[i].IndexOf("=", StringComparison.Ordinal) + 1);
            }
        }

        public void GetFieldAnsi(ReadOnlySpan<byte> template, List<FsdFieldSpec> fields)
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
            
                if (c != '?' && c != '$')
                    continue;

                if (template[i + 1] != '?' && template[i + 1] != '$')
                    continue;

                //Set our Position
                fields[currentField].FieldAnsi = ExtractFieldAnsi(template.Slice(i - 10, 10));
                currentField++;
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
            for (var i = fieldBytes.Length -1; i > 0; i--)
            {
                if (fieldBytes[i] == 0x1B)
                    return fieldBytes.Slice(i, (fieldBytes.Length - i)).ToArray();
            }

            return null;
        }
    }
}