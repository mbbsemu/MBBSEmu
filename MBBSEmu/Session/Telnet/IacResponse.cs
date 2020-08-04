using System.IO;

namespace MBBSEmu.Session.Telnet
{
    /// <summary>
    ///     Class used to assist with compiling IAC Responses
    ///
    ///     Takes enumerator inputs and returns a can return a byte array containing the
    ///     actual IAC sequence
    /// </summary>
    public class IacResponse
    {
        public EnumIacVerbs Verb;
        public EnumIacOptions Option;

        public IacResponse(EnumIacVerbs verb, EnumIacOptions option)
        {
            Verb = verb;
            Option = option;
        }

        public byte[] ToArray()
        {
            using var msOutput = new MemoryStream(3);
            msOutput.WriteByte(0xFF);
            msOutput.WriteByte((byte)Verb);
            msOutput.WriteByte((byte)Option);
            return msOutput.ToArray();
        }
    }
}
