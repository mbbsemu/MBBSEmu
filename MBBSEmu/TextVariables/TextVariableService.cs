using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;

namespace MBBSEmu.TextVariables
{
    public class TextVariableService : ITextVariableService
    {
        /// <summary>
        ///     Holds the list of Text Variables registered by the system
        /// </summary>
        private readonly List<TextVariable> _textVariables;

        private readonly ILogger _logger;

        public TextVariableService(ILogger logger)
        {
            _textVariables = new List<TextVariable>();
            _logger = logger;
        }

        /// <summary>
        ///     Sets the specified variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetVariable(string name, TextVariable.TextVariableValueDelegate value)
        {
            if (_textVariables.Any(x => x.Name == name))
                return;

            _textVariables.Add(new TextVariable() { Name = name, Value = value});
        }

        /// <summary>
        ///     Gets the specified variable value by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetVariableByName(string name) => _textVariables.FirstOrDefault(x => x.Name == name)?.Value();

        /// <summary>
        ///     Gets the specified variable value by index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string GetVariableByIndex(int index) => _textVariables[index].Value();

        /// <summary>
        ///     Gets the index of the specified variable by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetVariableIndex(string name)
        {
            for (var i = 0; i < _textVariables.Count; i++)
            {
                if (_textVariables[i].Name == name)
                    return i;
            }

            return -1;
        }

        /// <summary>
        ///     Parses incoming buffer to process text variables before sending to client
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionValues"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, TextVariable.TextVariableValueDelegate> sessionValues)
        {
            using var newOutputBuffer = new MemoryStream(input.Length);
            for (var i = 0; i < input.Length; i++)
            {
                //Look for initial signature byte -- faster
                if (input[i] != 0x1)
                {
                    newOutputBuffer.WriteByte(input[i]);
                    continue;
                }

                //If we found a 0x1 -- but it'd take us past the end of the buffer, we're done
                if (i + 3 >= input.Length)
                    break;

                //Look for justification notation in i + 1 = "N, L, C, R", if not found move on
                if (input[i + 1] != 0x4E && input[i + 1] != 0x4C && input[i + 1] != 0x43 && input[i + 1] != 0x52)
                    continue;

                //Get formatting information
                var variableFormatJustification = input[i + 1];
                var variableFormatPadding = (input[i + 2] - 32);

                //Increment 3 Bytes
                i += 3;

                var variableNameStart = i;
                var variableNameLength = 0;

                //Get variable name
                while (input[i] != 0x1)
                {
                    i++;
                    variableNameLength++;
                }

                var variableName = Encoding.ASCII.GetString(input.Slice(variableNameStart, variableNameLength));
                var variableText = GetVariableByName(variableName);

                //If not found, try Session specific Text Variables and show error if not
                if (variableText == null)
                {
                    switch (variableName)
                    {
                        case "CHANNEL":
                        case "USERID":
                            variableText = sessionValues[variableName]();
                            break;
                        default:
                            variableText = "UNKNOWN";
                            _logger.Error($"Unknown Text Variable: {variableName}");
                            break;
                    }
                }

                //Format Variable Text
                switch (variableFormatJustification)
                {
                    case 78:
                        //No formatting
                        break;
                    case 76:
                        //Left Justify
                        if (variableFormatPadding > variableText.Length)
                            variableText = variableText.PadRight(variableFormatPadding);
                        break;
                    case 67:
                        //Center Justify
                        if (variableFormatPadding > variableText.Length)
                        {
                            var centerPadLeft = (variableFormatPadding - variableText.Length) / 2;
                            var variableTextTemp = variableText.PadLeft(variableText.Length + centerPadLeft);
                            variableText = variableTextTemp.PadRight(variableFormatPadding);
                        }
                        break;
                    case 82:
                        //Right Justify
                        if (variableFormatPadding > variableText.Length)
                            variableText = variableText.PadLeft(variableFormatPadding);
                        break;
                    default:
                        _logger.Error($"Unknown Formatting for Variable: {variableName}");
                        break;
                }

                newOutputBuffer.Write(Encoding.ASCII.GetBytes(variableText));
            }

            return newOutputBuffer.ToArray();
        }
    }
}
