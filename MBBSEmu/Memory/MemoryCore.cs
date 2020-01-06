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

        public MemoryCore()
        {
            _memorySegments = new Dictionary<ushort, byte[]>();
            _segments = new Dictionary<ushort, Segment>();
            _decompiledSegments = new Dictionary<ushort, Dictionary<ushort, Instruction>>();
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

        public void AddSegment(EnumHostSegments segment) => AddSegment((ushort) segment);

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

        public byte GetByte(ushort segment, ushort offset)
        {
            return _memorySegments[segment][offset];
        }

        public ushort GetWord(ushort segment, ushort offset)
        {
            return BitConverter.ToUInt16(_memorySegments[segment], offset);
        }

        public byte[] GetArray(ushort segment, ushort offset, ushort count) => GetSpan(segment, offset, count).ToArray();

        public ReadOnlySpan<byte> GetSpan(ushort segment, ushort offset, ushort count)
        {
            Span<byte> segmentSpan = _memorySegments[segment];
            return segmentSpan.Slice(offset, count);
        }

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

        public void SetByte(ushort segment, ushort offset, byte value)
        {
            _memorySegments[segment][offset] = value;
        }

        public void SetWord(ushort segment, ushort offset, ushort value)
        {
            SetArray(segment, offset, BitConverter.GetBytes(value));
        }

        public void SetArray(ushort segment, ushort offset, byte[] array)
        {
            Array.Copy(array, 0, _memorySegments[segment], offset, array.Length);
        }

        public void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {
            SetArray(segment, offset, array.ToArray());
        }
    }
}
