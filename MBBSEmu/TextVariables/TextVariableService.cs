using MBBSEmu.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu.TextVariables
{
    public class TextVariableService : ITextVariableService
    {
        /// <summary>
        ///     Holds the list of Text Variables registered by the system
        /// </summary>
        private readonly List<TextVariableValue> _textVariables;

        private readonly IMessageLogger _logger;

        public TextVariableService(IMessageLogger logger)
        {
            _textVariables = new List<TextVariableValue>();
            _logger = logger;
        }

        /// <summary>
        ///     Sets the specified variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetVariable(string name, TextVariableValue.TextVariableValueDelegate value)
        {
            if (_textVariables.Any(x => x.Name == name))
                return;

            _textVariables.Add(new TextVariableValue() { Name = name, Value = value});
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
        ///     Looks for Variable Signature Byte of 0x1 and returns true
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public bool HasVariable(ReadOnlySpan<byte> input) => input.IndexOf((byte) 1) > -1;

        /// <summary>
        ///     Extracts Text Variable names found in the specified input
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public List<TextVariableDefinition> ExtractVariableDefinitions(ReadOnlySpan<byte> input)
        {
            var output = new List<TextVariableDefinition>();
            for (ushort i = 0; i < input.Length; i++)
            {
                //Look for initial signature byte -- faster
                if (input[i] != 0x1)
                    continue;

                //If we found a 0x1 -- but it'd take us past the end of the buffer, we're done
                if (i + 3 >= input.Length)
                    break;

                //Look for justification notation in i + 1 = "N, L, C, R", if not found move on
                if (input[i + 1] != 0x4E && input[i + 1] != 0x4C && input[i + 1] != 0x43 && input[i + 1] != 0x52)
                    continue;

                var startingOffset = i;

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

                output.Add(new TextVariableDefinition()
                {
                    Offset = startingOffset,
                    Length = (ushort)(i - startingOffset),
                    Justification = (EnumTextVariableJustification)variableFormatJustification,
                    Name = Encoding.ASCII.GetString(input.Slice(variableNameStart, variableNameLength)),
                    Padding = (byte)variableFormatPadding
                });


            }

            return output;
        }
        
        /// <summary>
        ///     Parses incoming buffer to process text variables before sending to client
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionValues"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, string> sessionValues)
        {
            using var newOutputBuffer = new MemoryStream(input.Length);

            var foundVariables = ExtractVariableDefinitions(input);

            var previousOffset = 0;
            foreach (var v in foundVariables)
            {
                //Add Every Byte from the previous offset to the byte just before the variable
                newOutputBuffer.Write(input.Slice(previousOffset, v.Offset - previousOffset));

                //Check Passed In Variables
                var variableText = sessionValues.FirstOrDefault(x=> x.Key == v.Name).Value;

                //If a Session Variable wasn't found, check global
                if(string.IsNullOrEmpty(variableText))
                    variableText = GetVariableByName(v.Name) ?? "UNKNOWN VARIABLE";

                //Format Variable Text
                switch (v.Justification)
                {
                    case EnumTextVariableJustification.None:
                        //No formatting
                        break;
                    case EnumTextVariableJustification.Left:
                        //Left Justify
                        if (v.Padding > variableText.Length)
                            variableText = variableText.PadRight(v.Padding);
                        break;
                    case EnumTextVariableJustification.Center:
                        //Center Justify
                        if (v.Padding > variableText.Length)
                        {
                            var centerPadLeft = (v.Padding - variableText.Length) / 2;
                            var variableTextTemp = variableText.PadLeft(variableText.Length + centerPadLeft);
                            variableText = variableTextTemp.PadRight(v.Padding);
                        }
                        break;
                    case EnumTextVariableJustification.Right:
                        //Right Justify
                        if (v.Padding > variableText.Length)
                            variableText = variableText.PadLeft(v.Padding);
                        break;
                    default:
                        _logger.Error($"Unknown Formatting for Variable: {v.Name}");
                        break;
                }

                newOutputBuffer.Write(Encoding.ASCII.GetBytes(variableText));
                previousOffset = v.Offset + v.Length + 1;
            }
            
            //Add Any Remaining
            newOutputBuffer.Write(input.Slice(previousOffset));

            return newOutputBuffer.ToArray();
        }

        /// <summary>
        ///     Parses incoming buffer to process text variables before sending to client
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionValues"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, TextVariableValue.TextVariableValueDelegate> sessionValues)
        {
            //Evaluate Delegates and Save them to a Local string,string Dictionary
            var invokedInput = sessionValues.ToDictionary(val => val.Key, val => val.Value());

            //Execute
            return Parse(input, invokedInput);
        }
    }
}
