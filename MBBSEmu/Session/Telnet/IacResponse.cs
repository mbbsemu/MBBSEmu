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

        public override bool Equals(object obj)
        {
            return Equals(obj as IacResponse);
        }

        public bool Equals(IacResponse other)
        {
            if (other == null)
                return false;

            return (Verb == other.Verb && Option == other.Option);
        }

        public override int GetHashCode()
        {
            return Verb.GetHashCode() ^ Option.GetHashCode();
        }
    }
}
