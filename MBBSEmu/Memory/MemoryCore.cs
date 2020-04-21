using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using Decoder = Iced.Intel.Decoder;


namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Handles Memory Operations for the Module
    ///
    ///     Information of x86 Memory Segmentation: https://en.wikipedia.org/wiki/X86_memory_segmentation
    /// </summary>
    public class MemoryCore : IMemoryCore
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        private readonly Dictionary<ushort, byte[]> _memorySegments;
        private readonly Dictionary<ushort, Segment> _segments;
        private readonly Dictionary<ushort, Dictionary<ushort, Instruction>> _decompiledSegments;

        private readonly Dictionary<string, IntPtr16> _variablePointerDictionary;
        private readonly IntPtr16 _currentVariablePointer;
        private const ushort VARIABLE_BASE = 0x100;

        private readonly PointerDictionary<Dictionary<ushort, IntPtr16>> _bigMemoryBlocks;


        public MemoryCore()
        {
            _memorySegments = new Dictionary<ushort, byte[]>();
            _segments = new Dictionary<ushort, Segment>();
            _decompiledSegments = new Dictionary<ushort, Dictionary<ushort, Instruction>>();
            _variablePointerDictionary = new Dictionary<string, IntPtr16>();
            _currentVariablePointer = new IntPtr16(VARIABLE_BASE, 0);

            _bigMemoryBlocks = new PointerDictionary<Dictionary<ushort, IntPtr16>>();

            //Add Segment 0 by default, stack segment
            AddSegment(0);
        }



        /// <summary>
        ///     Clears out the Memory Core and sets all values back to initial state
        /// </summary>
        public void Clear()
        {
            _memorySegments.Clear();
            _segments.Clear();
            _decompiledSegments.Clear();
            _variablePointerDictionary.Clear();
            _currentVariablePointer.Segment = VARIABLE_BASE;
            _currentVariablePointer.Offset = 0;
        }

        

        public IntPtr16 AllocateVariable(string name, ushort size, bool declarePointer = false)
        {
            if (!string.IsNullOrEmpty(name) && _variablePointerDictionary.ContainsKey(name))
            {
                _logger.Warn($"Attmped to re-allocate variable: {name}");
                return _variablePointerDictionary[name];
            }

            //Do we have enough room in the current segment?
            //If not, declare a new segment and start there
            if (size + _currentVariablePointer.Offset >= ushort.MaxValue)
            {
                _currentVariablePointer.Segment++;
                _currentVariablePointer.Offset = 0;
                AddSegment(_currentVariablePointer.Segment);
            }

            if (!HasSegment(_currentVariablePointer.Segment))
                AddSegment(_currentVariablePointer.Segment);

#if DEBUG
            _logger.Debug(
                $"Variable {name ?? "NULL"} allocated {size} bytes of memory in Host Memory Segment {_currentVariablePointer.Segment:X4}:{_currentVariablePointer.Offset:X4}");
#endif
            var currentOffset = _currentVariablePointer.Offset;
            _currentVariablePointer.Offset += (ushort)(size + 1);

            var newPointer = new IntPtr16(_currentVariablePointer.Segment, currentOffset);

            if(declarePointer && string.IsNullOrEmpty(name))
                throw new Exception("Unsupported operation, declaring pointer type for NULL named variable");

            if (!string.IsNullOrEmpty(name))
            {
                _variablePointerDictionary[name] = newPointer;

                if (declarePointer)
                {
                    var variablePointer = AllocateVariable($"*{name}", 0x4, false);
                    SetArray(variablePointer, newPointer.ToSpan());
                }
            }

            return newPointer;
        }

        public IntPtr16 GetVariablePointer(string name)
        {
            if (!TryGetVariablePointer(name, out var result))
                throw new ArgumentException($"Unknown Variable: {name}");

            return result;
        }

        public bool TryGetVariablePointer(string name, out IntPtr16 pointer)
        {
            if (!_variablePointerDictionary.TryGetValue(name, out var result))
            {
                pointer = null;
                return false;
            }

            pointer = result;
            return true;
        }

        public void SetVariable(string name, byte value) => SetByte(GetVariablePointer(name), value);
        public void SetVariable(string name, ushort value) => SetWord(GetVariablePointer(name), value);
        public void SetVariable(string name, uint value) => SetArray(GetVariablePointer(name), BitConverter.GetBytes(value));
        public void SetVariable(string name, ReadOnlySpan<byte> value) => SetArray(GetVariablePointer(name), value);
        public void SetVariable(string name, IntPtr16 value) => SetArray(GetVariablePointer(name), value.ToSpan());

        /// <summary>
        ///     Declares a new 16-bit Segment and allocates it to the defined Segment Number
        /// </summary>
        /// <param name="segmentNumber"></param>
        public void AddSegment(ushort segmentNumber, int size = 0x10000)
        {
            if(_memorySegments.ContainsKey(segmentNumber))
                throw new Exception($"Segment with number {segmentNumber} already defined");

            _memorySegments[segmentNumber] = new byte[size];
        }

        public void AddSegment(Segment segment)
        {
            //Get Address for this Segment
            var segmentMemory = new byte[0x10000];
            
            //Add the data to memory and record the segment offset in memory
            Array.Copy(segment.Data, 0, segmentMemory, 0, segment.Data.Length);
            _memorySegments.Add(segment.Ordinal, segmentMemory);

            if (segment.Flags.Contains(EnumSegmentFlags.Code))
            {
                //Decode the Segment
                var instructionList = new InstructionList();
                var codeReader = new ByteArrayCodeReader(segment.Data);
                var decoder = Decoder.Create(16, codeReader);
                decoder.IP = 0x0;

                while (decoder.IP < (ulong)segment.Data.Length)
                {
                    decoder.Decode(out instructionList.AllocUninitializedElement());
                }

                _decompiledSegments.Add(segment.Ordinal, new Dictionary<ushort, Instruction>());
                foreach (var i in instructionList)
                {
                    _decompiledSegments[segment.Ordinal].Add(i.IP16, i);
                }
            }

            _segments[segment.Ordinal] = segment;
        }

        /// <summary>
        ///     Adds the specified Segment with the specified InstructionList
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentInstructionList"></param>
        public void AddSegment(ushort segmentNumber, InstructionList segmentInstructionList)
        {
            _decompiledSegments.Add(segmentNumber, new Dictionary<ushort, Instruction>());
            foreach (var i in segmentInstructionList)
            {
                _decompiledSegments[segmentNumber].Add(i.IP16, i);
            }
        }

        public Segment GetSegment(ushort segmentNumber) => _segments[segmentNumber];

        public bool HasSegment(ushort segmentNumber) => _memorySegments.ContainsKey(segmentNumber);

        public Instruction GetInstruction(ushort segment, ushort instructionPointer)
        {
            //If it wasn't able to decompile linear through the data, there might have been
            //data in the path of the code that messed up decoding, in this case, we grab up to
            //6 bytes at the IP and decode the instruction manually. This works 9 times out of 10
            if(!_decompiledSegments[segment].TryGetValue(instructionPointer, out var outputInstruction))
            {
                Span<byte> segmentData = _segments[segment].Data;
                var reader = new ByteArrayCodeReader(segmentData.Slice(instructionPointer, 6).ToArray());
                var decoder = Decoder.Create(16, reader);
                decoder.IP = instructionPointer;
                decoder.Decode(out var instr);

                return instr;
            }
            return outputInstruction;
        }

        public byte GetByte(IntPtr16 pointer) => GetByte(pointer.Segment, pointer.Offset);

        public byte GetByte(ushort segment, ushort offset)
        {
            if (!_memorySegments.TryGetValue(segment, out var selectedSegment))
                throw new ArgumentOutOfRangeException($"Unable to locate {segment:X4}:{offset:X4}");

            return selectedSegment[offset];
        }

        public ushort GetWord(IntPtr16 pointer) => GetWord(pointer.Segment, pointer.Offset);

        public ushort GetWord(ushort segment, ushort offset)
        {
            if(!_memorySegments.TryGetValue(segment, out var selectedSegment))
                throw new ArgumentOutOfRangeException($"Unable to locate {segment:X4}:{offset:X4}");

            return BitConverter.ToUInt16(selectedSegment, offset);
        }

        public IntPtr16 GetPointer(IntPtr16 pointer) => new IntPtr16(GetArray(pointer, 4));
        

        public IntPtr16 GetPointer(ushort segment, ushort offset) => new IntPtr16(GetArray(segment, offset, 4));

        public ReadOnlySpan<byte> GetArray(IntPtr16 pointer, ushort count) =>
            GetArray(pointer.Segment, pointer.Offset, count);

        public ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count)
        {
            if (!_memorySegments.TryGetValue(segment, out var selectedSegment))
                throw new ArgumentOutOfRangeException($"Unable to locate {segment:X4}:{offset:X4}");

            ReadOnlySpan<byte> segmentSpan = selectedSegment;
            return segmentSpan.Slice(offset, count);
        }

        public ReadOnlySpan<byte> GetString(IntPtr16 pointer, bool stripNull = false) =>
            GetString(pointer.Segment, pointer.Offset, stripNull);

        /// <summary>
        ///     Reads an array of bytes from the specified segment:offset, stopping
        ///     at the first null character denoting the end of the string.
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false)
        {
            if (!_memorySegments.TryGetValue(segment, out var selectedSegment))
            {
                _logger.Error($"Invalid Pointer -> {segment:X4}:{offset:X4}");
                return Encoding.ASCII.GetBytes("Invalid Pointer");
            }

            ReadOnlySpan<byte> segmentSpan = selectedSegment;

            for (var i = 0; i < ushort.MaxValue; i++)
            {
                if (segmentSpan[offset + i] == 0x0)
                    return segmentSpan.Slice(offset, i + (stripNull ? 0 : 1));
            }

            throw new Exception($"Invalid String at {segment:X4}:{offset:X4}");
        }

        public void SetByte(IntPtr16 pointer, byte value) => SetByte(pointer.Segment, pointer.Offset, value);

        public void SetByte(ushort segment, ushort offset, byte value)
        {
            _memorySegments[segment][offset] = value;
        }

        public void SetWord(IntPtr16 pointer, ushort value) => SetWord(pointer.Segment, pointer.Offset, value);

        public void SetWord(ushort segment, ushort offset, ushort value) => SetArray(segment, offset, BitConverter.GetBytes(value));
        

        public void SetArray(IntPtr16 pointer, ReadOnlySpan<byte> array) => SetArray(pointer.Segment, pointer.Offset, array);

        public void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {

#if DEBUG
            for (var i = 0; i < array.Length; i++)
            {
                _memorySegments[segment][offset + i] = array[i];
            }
#else
            Array.Copy(array.ToArray(), 0, _memorySegments[segment], offset, array.Length);
#endif
        }


        public void SetPointer(IntPtr16 pointer, IntPtr16 value) => SetArray(pointer, value.ToSpan());

        public IntPtr16 AllocateBigMemoryBlock(ushort quantity, ushort size)
        {
            var newBlockOffset = _bigMemoryBlocks.Allocate(new Dictionary<ushort, IntPtr16>());

            //Fill the Region
            for (ushort i = 0; i < quantity; i++)
                _bigMemoryBlocks[newBlockOffset].Add(i, AllocateVariable($"ALCBLOK-{newBlockOffset}-{i}", size));

            return new IntPtr16(0xFFFF, (ushort)newBlockOffset);
        }

        public IntPtr16 GetBigMemoryBlock(IntPtr16 block, ushort index) => _bigMemoryBlocks[block.Offset][index];
    }
}
