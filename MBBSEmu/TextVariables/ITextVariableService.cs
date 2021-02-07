using System;
using System.Collections.Generic;

namespace MBBSEmu.TextVariables
{
    public interface ITextVariableService
    {
        /// <summary>
        ///     Sets the specified variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void SetVariable(string name, TextVariable.TextVariableValueDelegate value);

        /// <summary>
        ///     Gets the specified variable value by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string GetVariableByName(string name);

        /// <summary>
        ///     Gets the specified variable value by index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        string GetVariableByIndex(int index);

        /// <summary>
        ///     Gets the index of the specified variable by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        int GetVariableIndex(string name);

        /// <summary>
        ///     Parses incoming buffer to process text variables before sending to client
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionValues"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, TextVariable.TextVariableValueDelegate> sessionValues);

        ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, string> sessionValues);

        bool HasVariable(ReadOnlySpan<byte> input);

        List<string> ExtractVariableNames(ReadOnlySpan<byte> input);
    }
}
