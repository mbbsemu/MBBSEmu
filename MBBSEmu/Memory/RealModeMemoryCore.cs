using Iced.Intel;
using NLog;
using System;
using System.Collections.Generic;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Represents DOS Real Mode memory.
    ///
    ///     Memory consists of 16-byte overlapped segments. Address 0000:0010 is the same as
    ///     address 0001:0000 and 2000:0050 is the same as 2005:0000. If you do the math, each
    ///     MSB in segment is 64k in length (0000-1000), of which you have 16, which yields a total
    ///     addressable memory space of 1MB.
    ///
    ///     We just allocate a 1MB memory block and map accordingly.
    ///
    ///     Memory is mapped with executable at the top of the address space up to 0xF000, so the
    ///     executable code should live directly below that address. The stack is immediately below
    ///     the start of the executable - growing down. There is a 64k heap at 0x1000-0x2000 which
    ///     grows up.
    /// </summary>
    public class RealModeMemoryCore : AbstractMemoryCore, IMemoryCore
    {
        public const int MAX_REAL_MODE_MEMORY = 1024 * 1024; // (1 mb)
        public const int HEAP_BASE_SEGMENT = 0x1000;
        private readonly FarPtr HEAP_BASE = new FarPtr(HEAP_BASE_SEGMENT, 0);
        private const int HEAP_MAX_SIZE = 64*1024;

        private readonly byte[] _memory = new byte[MAX_REAL_MODE_MEMORY];

        private MemoryAllocator _memoryAllocator;

        private readonly Dictionary<int, Instruction> _instructionCache = new(128*1024);

        public RealModeMemoryCore(ILogger logger) : base(logger)
        {
            // fill with halt instructions
            Array.Fill(_memory, (byte)0xF4);

            // set alignment to 16 so that all allocations return a clean segment (which are 16 bytes)
            _memoryAllocator = new MemoryAllocator(logger, HEAP_BASE, HEAP_MAX_SIZE, alignment: 16);
        }

        public override Span<byte> VirtualToPhysical(ushort segment, ushort offset) => _memory.AsSpan((segment << 4) + offset);

        public override FarPtr AllocateRealModeSegment(ushort segmentSize = ushort.MaxValue) => throw new NotImplementedException();
        public override FarPtr AllocateBigMemoryBlock(ushort quantity, ushort size) => throw new NotImplementedException();
        public override FarPtr GetBigMemoryBlock(FarPtr block, ushort index) => throw new NotImplementedException();

        public override FarPtr Malloc(uint size)
        {
            var ptr = _memoryAllocator.Malloc(size);
            if (ptr.IsNull())
                return ptr;

            // ptr is returned with segment = 0x1000 and an offset, so change ptr to have 0 offset
            // by incrementing segment.
            return new FarPtr((ushort)(ptr.Segment + (ptr.Offset >> 4)), 0);
        }
        public override void Free(FarPtr ptr)
        {
            // ptr should have 0 offset, but we need to reconvert back to segment 0x1000 base.
            _memoryAllocator.Free(new FarPtr(HEAP_BASE_SEGMENT, (ushort)(ptr.Offset + ((ptr.Segment - HEAP_BASE_SEGMENT) << 4))));
        }

        public Instruction GetInstruction(ushort segment, ushort instructionPointer)
        {
            var physicalAddress = VirtualToPhysicalAddress(segment, instructionPointer);

            if (_instructionCache.TryGetValue(physicalAddress, out var instruction))
                return instruction;

            var codeReader = new ByteArrayCodeReader(VirtualToPhysical(segment, instructionPointer).Slice(0, 10).ToArray());
            var decoder = Decoder.Create(16, codeReader);
            decoder.IP = instructionPointer;

            instruction = decoder.Decode();
            _instructionCache.Add(VirtualToPhysicalAddress(segment, instructionPointer), instruction);
            return instruction;
        }

        public Instruction Recompile(ushort segment, ushort instructionPointer)
        {
            _instructionCache.Remove(VirtualToPhysicalAddress(segment, instructionPointer));
            return GetInstruction(segment, instructionPointer);
        }

        public static int VirtualToPhysicalAddress(ushort segment, ushort offset) => ((segment << 4) + offset);
        public static FarPtr PhysicalToVirtualAddress(int offset) => new FarPtr((ushort)(offset >> 4), (ushort)(offset & 0xF));

        public override void Clear()
        {
            base.Clear();

            _instructionCache.Clear();
            _memoryAllocator = new MemoryAllocator(_logger, HEAP_BASE, HEAP_MAX_SIZE, alignment: 16);
            Array.Fill(_memory, (byte)0);
        }
    }
}
