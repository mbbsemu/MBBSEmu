using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog.Targets;

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
                var newSpec = new FsdFieldSpec() {FsdFieldType = EnumFsdFieldType.Text};

                if (fieldSpec[i] == ' ')
                    continue;

                //Extract Name
                for (var n = i; n < fieldSpec.Length; n++)
                {
                    if (char.IsLetter((char) fieldSpec[n]))
                        continue;

                    newSpec.Name = Encoding.ASCII.GetString(fieldSpec.Slice(i, n - i));

                    if (fieldSpec[n] == ' ')
                        n++;

                    i = n;
                    break;
                }

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
                                newSpec.LengthMin = int.Parse(fieldOption[1]);
                                break;
                            case "MAX":
                                newSpec.LengthMax = int.Parse(fieldOption[1]);
                                break;
                            case "SECRET":
                                newSpec.FsdFieldType = EnumFsdFieldType.Secret;
                                break;
                            case "MULTICHOICE":
                                newSpec.FsdFieldType = EnumFsdFieldType.MultipleChoice;
                                break;
                        }

                        i = o;

                        if (fieldSpec[o] == ')')
                            break;
                    }
                }
                result.Add(newSpec);
            }

            return result;
        }
    }
}
