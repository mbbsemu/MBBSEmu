namespace MBBSEmu.TextVariables
{
    /// <summary>
    ///     Defines a Text Variable within an output string (MSG, cstring, etc.)
    /// </summary>
    public class TextVariableDefinition
    {
        public ushort Offset { get; set; }
        public ushort Length { get; set; }
        public string Name { get; set; }
        public EnumTextVariableJustification Justification { get; set; }
        public byte Padding { get; set; }
    }
}
