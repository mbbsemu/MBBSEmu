using Iced.Intel;
using Decoder = Iced.Intel.Decoder;


namespace MBBSEmu.Disassembler
{
    public class ModuleDisassembly
    {
        private readonly byte[] _moduleData;
        private readonly Decoder _decoder;
        private readonly ByteArrayCodeReader _codeReader;
        public readonly InstructionList Instructions;


        public ModuleDisassembly(byte[] moduleData)
        {
            _moduleData = moduleData;
            _codeReader = new ByteArrayCodeReader(_moduleData);
            _decoder = Decoder.Create(16, _codeReader);
            _decoder.IP = 0x0;

            Instructions = new InstructionList();
            
        }

        public void Disassemble()
        {
            while (_decoder.IP < (ulong) _moduleData.Length)
            {
                _decoder.Decode(out Instructions.AllocUninitializedElement());
            }
        }
    }
}
