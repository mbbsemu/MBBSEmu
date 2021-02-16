namespace MBBSEmu.TextVariables
{
    public class TextVariableValue
    {
        public delegate string TextVariableValueDelegate();

        public string Name { get; set; }
        public TextVariableValueDelegate Value { get; set; }
    }
}
