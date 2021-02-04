namespace MBBSEmu.TextVariables
{
    public class TextVariable
    {
        public delegate string TextVariableValueDelegate();

        public string Name { get; set; }
        public TextVariableValueDelegate Value { get; set; }
    }
}
