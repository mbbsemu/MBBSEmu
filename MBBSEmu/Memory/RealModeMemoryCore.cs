using Iced.Intel;
using MBBSEmu.Logging;
using System;

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
        /// <summary>
        ///     Singleton Instance of ProtectedModeMemoryCore
        /// </summary>
        private static RealModeMemoryCore _instance;
        
        /// <summary>
        ///     Thread Safety Lock for Singleton
        /// </summary>
        private static readonly object _lock = new object();
        
        public const int MAX_REAL_MODE_MEMORY = 1024 * 1024; // (1 mb)
        public const int HEAP_MAX_SIZE = 64*1024;
        public const ushort DEFAULT_HEAP_BASE_SEGMENT = 0x8000;

        public readonly ushort _heapBaseSegment;
        private readonly byte[] _memory = new byte[MAX_REAL_MODE_MEMORY];

        private MemoryAllocator _memoryAllocator;

        /// <summary>
        ///     CodeReader implementation which feeds the Iced.Intel
        ///     decoder based on our current IP.
        /// </summary>
        private class RealModeCodeReader : CodeReader
        {
            private readonly byte[] _memory;
            private int ip = 0;

            public RealModeCodeReader(byte[] memory)
            {
                _memory = memory;
            }

            public override int ReadByte() => _memory[ip++];

            public void SetCurrent(int position) => ip = position;
        }

        private readonly RealModeCodeReader _codeReader;

        private readonly Decoder _decoder;

        // Public method to provide a global point of access to the instance
        public static RealModeMemoryCore GetInstance(ushort heapBaseSegment, IMessageLogger logger)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new RealModeMemoryCore(heapBaseSegment, logger);
                }
            }
            
            //Parameter Sanity Check
            if(heapBaseSegment != _instance._heapBaseSegment || logger.GetHashCode() != _instance.GetHashCode())
                throw new InvalidOperationException($"Cannot create instance of {nameof(RealModeMemoryCore)}: Parameter Mismatch");
            
            return _instance;
        }
        
        public RealModeMemoryCore(ushort heapBaseSegment, IMessageLogger logger) : base(logger)
        {
            _heapBaseSegment = heapBaseSegment;

            _codeReader = new(_memory);
            _decoder = Decoder.Create(16, _codeReader);

            // fill with halt instructions
            Array.Fill(_memory, (byte)0);

            // set alignment to 16 so that all allocations return a clean segment (which are 16 bytes)
            _memoryAllocator = new MemoryAllocator(logger, new FarPtr(_heapBaseSegment, 0), HEAP_MAX_SIZE, alignment: 16);
        }

        public static RealModeMemoryCore GetInstance(IMessageLogger logger) =>
            GetInstance(DEFAULT_HEAP_BASE_SEGMENT, logger);

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
            if (ptr.IsNull())
                return;

            // ptr should have 0 offset, but we need to reconvert back to segment 0x1000 base.
            var adjustedPtr = new FarPtr(_heapBaseSegment, (ushort)(ptr.Offset + ((ptr.Segment - _heapBaseSegment) << 4)));
            _memoryAllocator.Free(adjustedPtr);
        }

        public int GetAllocatedMemorySize(FarPtr ptr)
        {
            // ptr should have 0 offset, but we need to reconvert back to segment 0x1000 base.
            var adjustedPtr = new FarPtr(_heapBaseSegment, (ushort)(ptr.Offset + ((ptr.Segment - _heapBaseSegment) << 4)));
            return _memoryAllocator.GetAllocatedMemorySize(adjustedPtr);
        }

        public Instruction GetInstruction(ushort segment, ushort instructionPointer)
        {
            var physicalAddress = VirtualToPhysicalAddress(segment, instructionPointer);
            _codeReader.SetCurrent(physicalAddress);

            _decoder.IP = instructionPointer;
            return _decoder.Decode();
        }

        public Instruction Recompile(ushort segment, ushort instructionPointer) => GetInstruction(segment, instructionPointer);

        public static int VirtualToPhysicalAddress(ushort segment, ushort offset) => ((segment << 4) + offset);
        public static FarPtr PhysicalToVirtualAddress(int offset) => new FarPtr((ushort)(offset >> 4), (ushort)(offset & 0xF));

        public override void Clear()
        {
            base.Clear();

            _memoryAllocator = new MemoryAllocator(_logger, new FarPtr(_heapBaseSegment, 0), HEAP_MAX_SIZE, alignment: 16);
            Array.Fill(_memory, (byte)0);
        }

        public ReadOnlySpan<byte> GetMemorySegment(ushort segment) => _memory.AsSpan();
        public ushort CountSegments()
        {
            throw new NotImplementedException();
        }
    }
}