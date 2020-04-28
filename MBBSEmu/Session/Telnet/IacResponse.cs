using System.IO;

namespace MBBSEmu.Session.Telnet
{
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
