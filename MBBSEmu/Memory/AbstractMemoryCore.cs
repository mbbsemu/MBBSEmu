﻿using MBBSEmu.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Handles Memory Operations for the Module
    ///
    ///     Information of x86 Memory Segmentation: https://en.wikipedia.org/wiki/X86_memory_segmentation
    /// </summary>
    public abstract class AbstractMemoryCore
    {
        /// <summary>
        ///     Default Compiler Hints for use on methods within the MemoryCore
        ///
        ///     Works fastest with just AggressiveOptimization. Enabling AggressiveInlining slowed
        ///     down the code.
        /// </summary>
        protected const MethodImplOptions CompilerOptimizations = MethodImplOptions.AggressiveOptimization;

        protected IMessageLogger _logger;
        private readonly Dictionary<string, FarPtr> _variablePointerDictionary = new();

        public AbstractMemoryCore(IMessageLogger logger)
        {
            _logger = logger;
        }

        public abstract FarPtr Malloc(uint size);

        public abstract void Free(FarPtr ptr);

        /// <summary>
        ///     Deletes all data from Memory
        /// </summary>
        public virtual void Clear()
        {
            _variablePointerDictionary.Clear();
        }

        /// <summary>
        ///     Allocates the specified variable name with the desired size
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer"></param>
        /// <returns></returns>
        public FarPtr AllocateVariable(string name, ushort size, bool declarePointer = false)
        {
            if (!string.IsNullOrEmpty(name) && _variablePointerDictionary.ContainsKey(name))
            {
                _logger.Warn($"Attempted to re-allocate variable: {name}");
                return _variablePointerDictionary[name];
            }

            var newPointer = Malloc(size);
            (this as IMemoryCore).SetZero(newPointer, size);

            if (declarePointer && string.IsNullOrEmpty(name))
                throw new ArgumentException("Unsupported operation, declaring pointer type for NULL named variable");

            if (!string.IsNullOrEmpty(name))
            {
                _variablePointerDictionary[name] = newPointer;

                if (declarePointer)
                {
                    var variablePointer = AllocateVariable($"*{name}", 0x4, declarePointer: false);
                    (this as IMemoryCore).SetArray(variablePointer, newPointer.Data);
                }
            }

            return newPointer;
        }

        /// <summary>
        ///     Returns the pointer to a defined variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public FarPtr GetVariablePointer(string name)
        {
            if (!TryGetVariablePointer(name, out var result))
                throw new ArgumentException($"Unknown Variable: {name}");

            return result;
        }

        /// <summary>
        ///     Safe retrieval of a pointer to a defined variable
        ///
        ///     Returns false if the variable isn't defined
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pointer"></param>
        /// <returns></returns>
        public bool TryGetVariablePointer(string name, out FarPtr pointer)
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
        ///     Safely try to retrieve a variable, or allocate it if it's not present
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer">
        ///     Some variables are pointers to an underlying value. Setting this value to TRUE declares not only the
        ///     desired variable of NAME of SIZE, but also a 2 byte variable named "*NAME" which holds a pointer to NAME
        /// </param>
        /// <returns></returns>
        public FarPtr GetOrAllocateVariablePointer(string name, ushort size = 0x0, bool declarePointer = false)
        {
            if (_variablePointerDictionary.TryGetValue(name, out var result))
                return result;

            return AllocateVariable(name, size, declarePointer);
        }

        public abstract Span<byte> VirtualToPhysical(ushort segment, ushort offset);

        /// <summary>
        ///     Returns a single byte from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public byte GetByte(ushort segment, ushort offset) => VirtualToPhysical(segment, offset)[0];

        /// <summary>
        ///     Returns an unsigned word from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public unsafe ushort GetWord(ushort segment, ushort offset) {
            fixed (byte *p = VirtualToPhysical(segment, offset))
            {
                return *((ushort*)p);
            }
        }

        [MethodImpl(CompilerOptimizations)]
        public unsafe uint GetDWord(ushort segment, ushort offset)
        {
            fixed (byte* p = VirtualToPhysical(segment, offset))
            {
                return *((uint*)p);
            }
        }

        /// <summary>
        ///     Returns an array with the desired count from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count) =>
            VirtualToPhysical(segment, offset).Slice(0, count);


        [MethodImpl(CompilerOptimizations)]
        public ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false)
        {
            var segmentSpan = VirtualToPhysical(segment, offset);
            var nullTerminator = segmentSpan.IndexOf((byte)0);
            if (nullTerminator < 0)
                throw new Exception($"Invalid String at {segment:X4}:{offset:X4}");

            return segmentSpan.Slice(0, nullTerminator + (stripNull ? 0 : 1));
        }

        /// <summary>
        ///     Sets the specified byte at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public void SetByte(ushort segment, ushort offset, byte value) => VirtualToPhysical(segment, offset)[0] = value;

        /// <summary>
        ///     Sets the specified word at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        [MethodImpl(CompilerOptimizations)]
        public unsafe void SetWord(ushort segment, ushort offset, ushort value)
        {
            fixed (byte *dst = VirtualToPhysical(segment, offset))
            {
                *((ushort*)dst) = value;
            }
        }

        [MethodImpl(CompilerOptimizations)]
        public unsafe void SetDWord(ushort segment, ushort offset, uint value)
        {
            fixed (byte* dst = VirtualToPhysical(segment, offset))
            {
                *((uint*)dst) = value;
            }
        }

        [MethodImpl(CompilerOptimizations)]
        public void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array)
        {
            var destinationSpan = VirtualToPhysical(segment, offset);
            array.CopyTo(destinationSpan);
        }

        /// <summary>
        ///     Writes the specified byte the specified number of times starting at the specified pointer
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        public void FillArray(ushort segment, ushort offset, int count, byte value)
        {
            VirtualToPhysical(segment, offset).Slice(0, count).Fill(value);
        }

        /// <summary>
        ///     Allocates the specific number of Big Memory Blocks with the desired size
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public abstract FarPtr AllocateBigMemoryBlock(ushort quantity, ushort size);

        public abstract FarPtr GetBigMemoryBlock(FarPtr block, ushort index);

        /// <summary>
        ///     Returns a newly allocated Segment in "Real Mode" memory
        /// </summary>
        /// <returns></returns>
        public abstract FarPtr AllocateRealModeSegment(ushort segmentSize = ushort.MaxValue);

        public void Dispose()
        {
            Clear();
        }
    }
}
