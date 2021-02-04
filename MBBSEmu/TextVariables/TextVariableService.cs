using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.TextVariables
{
    public class TextVariableService : ITextVariableService
    {

        /// <summary>
        ///     Holds the list of Text Variables registered by the system
        /// </summary>
        private readonly List<TextVariable> _textVariables;

        public TextVariableService()
        {
            _textVariables = new List<TextVariable>();
        }

        /// <summary>
        ///     Sets the specified variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetVariable(string name, string value)
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
        public string GetVariableByName(string name) => _textVariables.FirstOrDefault(x => x.Name == name)?.Value;

        /// <summary>
        ///     Gets the specified variable value by index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string GetVariableByIndex(int index) => _textVariables[index].Value;

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
    }
}
