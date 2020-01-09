using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using NLog;
using System;
using System.Collections.Generic;

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

        public MemoryCore()
        {
            _memorySegments = new Dictionary<ushort, byte[]>();
            _segments = new Dictionary<ushort, Segment>();
            _decompiledSegments = new Dictionary<ushort, Dictionary<ushort, Instruction>>();
            _variablePointerDictionary = new Dictionary<string, IntPtr16>();
            _currentVariablePointer = new IntPtr16(VARIABLE_BASE, 0);

            //Add Segment 0 by default, stack segment
            AddSegment(0);
        }

        public IntPtr16 AllocateVariable(string name, ushort size)
        {
            if (_variablePointerDictionary.ContainsKey(name))
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
                $"Variable {name} allocated {size} bytes of memory in Host Memory Segment {_currentVariablePointer.Segment:X4}:{_currentVariablePointer.Offset:X4}");
#endif
            var currentOffset = _currentVariablePointer.Offset;
            _currentVariablePointer.Offset += size;

            var newPointer = new IntPtr16(_currentVariablePointer.Segment, currentOffset);

            if (!string.IsNullOrEmpty(name))
                _variablePointerDictionary[name] = newPointer;

            return newPointer;
        }

        public IntPtr16 GetVariable(string name)
        {
            if (!TryGetVariable(name, out var result))
                throw new ArgumentException($"Unknown Variable: {name}");

            return result;
        }

        public bool TryGetVariable(string name, out IntPtr16 pointer)
        {
            if (!_variablePointerDictionary.TryGetValue(name, out var result))
            {
                pointer = null;
                return false;
            }

            pointer = result;
            return true;
        }

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

        public Segment GetSegment(ushort segmentNumber) => _segments[segmentNumber];

        public bool HasSegment(ushort segmentNumber) => _memorySegments.ContainsKey(segmentNumber);

        public Instruction GetInstruction(ushort segment, ushort instructionPointer)
        {
            if(!_decompiledSegments[segment].TryGetValue(instructionPointer, out var outputInstruction))
            {
                Span<byte> segmentData = _segments[segment].Data;
                switch (segmentData[instructionPointer])
                {
                    //Look for an ENTER opcode that might have been decoded improperly due to data in the code segment
                    case 0xC8:
                    {
                        var patchedInstruction = Instruction.Create(Code.Enterq_imm16_imm8);
                        patchedInstruction.ByteLength = 4;
                        patchedInstruction.IP16 = instructionPointer;
                        patchedInstruction.Op0Kind = OpKind.Immediate16;
                        patchedInstruction.Immediate16 = BitConverter.ToUInt16(segmentData.Slice(instructionPointer + 1, 2)); ;
                        return patchedInstruction;
                    }
                    case 0x55:
                    {
                        var patchedInstruction = Instruction.Create(Code.Push_r16);
                        patchedInstruction.ByteLength = 1;
                        patchedInstruction.IP16 = instructionPointer;
                        patchedInstruction.Op0Kind = OpKind.Register;
                        patchedInstruction.Op0Register = Register.BP;
                        return patchedInstruction;
                        }
                    case 0x56:
                    {
                        var patchedInstruction = Instruction.Create(Code.Push_r16);
                        patchedInstruction.ByteLength = 1;
                        patchedInstruction.IP16 = instructionPointer;
                        patchedInstruction.Op0Kind = OpKind.Register;
                        patchedInstruction.Op0Register = Register.SI;
                        return patchedInstruction;
                    }
                    case 0x57:
                    {
                        var patchedInstruction = Instruction.Create(Code.Push_r16);
                        patchedInstruction.ByteLength = 1;
                        patchedInstruction.IP16 = instructionPointer;
                        patchedInstruction.Op0Kind = OpKind.Register;
                        patchedInstruction.Op0Register = Register.DI;
                        return patchedInstruction;
                    }
                }
            }
            return outputInstruction;
        }

        public byte GetByte(IntPtr16 pointer) => GetByte(pointer.Segment, pointer.Offset);

        public byte GetByte(ushort segment, ushort offset)
        {
            return _memorySegments[segment][offset];
        }

        public ushort GetWord(IntPtr16 pointer) => GetWord(pointer.Segment, pointer.Offset);

        public ushort GetWord(ushort segment, ushort offset)
        {
            return BitConverter.ToUInt16(_memorySegments[segment], offset);
        }

        public ReadOnlySpan<byte> GetArray(IntPtr16 pointer, ushort count) =>
            GetArray(pointer.Segment, pointer.Offset, count);

        public ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count)
        {
            Span<byte> segmentSpan = _memorySegments[segment];
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
            Span<byte> segmentSpan = _memorySegments[segment];

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
            Array.Copy(array.ToArray(), 0, _memorySegments[segment], offset, array.Length);
        }
    }
}
