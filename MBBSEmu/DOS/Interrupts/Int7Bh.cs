using MBBSEmu.CPU;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Runtime.InteropServices;

namespace MBBSEmu.DOS.Interrupts
{
    public enum BtrieveError
    {
        Success = 0,
        InvalidOperation = 1,
        IOError = 2,
        FileNotOpen = 3,
        KeyValueNotFound = 4,
        DuplicateKeyValue = 5,
        InvalidKeyNumber = 6,
        DifferentKeyNumber = 7,
        InvalidPositioning = 8,
        EOF = 9 ,
    }

    // https://docs.actian.com/psql/psqlv13/index.html#page/btrieveapi/btrintro.htm
    // http://www.nomad.ee/btrieve/errors/index.shtml

    /// <summary>
    ///     Btrieve interrupt handler
    /// </summary>
    public class Int7Bh : IInterruptHandler
    {
        private readonly ILogger _logger;
        private readonly IMemoryCore _memory;
        private readonly ICpuRegisters _registers;

        public byte Vector => 0x7B;


        protected struct BtrieveCommand
        {
            public ushort data_buffer_offset;
            public ushort data_buffer_segment;

            public ushort data_buffer_length;

            public ushort position_block_offset;
            public ushort position_block_segment;

            public ushort fcb_offset;
            public ushort fcb_segment;

            public ushort operation;

            public ushort key_buffer_offset;
            public ushort key_buffer_segment;

            public byte key_buffer_length;

            public byte key_number;

            public ushort status_code_pointer_offset;
            public ushort status_code_pointer_segment;

            public ushort interface_id; // should always be 0x6176

            public void Log(ILogger logger)
            {
                /*foreach (var c in memory)
                {
                    _logger.Error($"Byte: {c:X2}");
                }*/


                logger.Error($"DataBufferSegment: {new FarPtr(data_buffer_segment, data_buffer_offset)}:{data_buffer_length}");
                logger.Error($"PosBlock: {new FarPtr(position_block_segment, position_block_offset)}");
                logger.Error($"FCB: {new FarPtr(fcb_segment, fcb_offset)}");
                logger.Error($"Operation: {operation}");
                logger.Error($"KeyBuffer: {new FarPtr(key_buffer_segment, key_buffer_offset)}:{key_buffer_length}");
            }
        }

        public Int7Bh(ILogger logger, ICpuRegisters registers, IMemoryCore memory)
        {
            _logger = logger;
            _registers = registers;
            _memory = memory;
        }

        public void Handle()
        {
            // DS:DX is argument
            var data = ByteArrayToStructure<BtrieveCommand>(_memory.GetArray(_registers.DS, _registers.DX, 28).ToArray());
            data.Log(_logger);

            if (data.interface_id != 0x6176)
                throw new ArgumentException($"Bad interface_id");

            _memory.SetWord(data.status_code_pointer_segment, data.status_code_pointer_offset, (ushort)BtrieveError.FileNotOpen);
            //_registers.CarryFlag = true;
            //throw new ArgumentException($"Btrieve operation {data.operation}");
        }

        unsafe T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            fixed (byte* ptr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
            }
        }
    }
}
