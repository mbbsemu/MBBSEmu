using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Interface for Memory Management within MBBSEmu
    /// </summary>
    public interface IMemoryCore
    {
        //*** Variable Memory Management *** 
        
        /// <summary>
        ///     Returns the pointer to a defined variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IntPtr16 GetVariablePointer(string name);
        
        /// <summary>
        ///     Safe retrieval of a pointer to a defined variable
        ///
        ///     Returns false if the variable isn't defined
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pointer"></param>
        /// <returns></returns>
        bool TryGetVariablePointer(string name, out IntPtr16 pointer);

        /// <summary>
        ///     Allocates the specified variable name with the desired size
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer"></param>
        /// <returns></returns>
        IntPtr16 AllocateVariable(string name, ushort size, bool declarePointer = false);
        
        /// <summary>
        ///     Allocates the specific number of Big Memory Blocks with the desired size
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        IntPtr16 AllocateBigMemoryBlock(ushort quantity, ushort size);
        
        /// <summary>
        ///     Returns the specified block by index in the desired memory block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        IntPtr16 GetBigMemoryBlock(IntPtr16 block, ushort index);

        //*** Segment Management *** 

        /// <summary>
        ///     Adds a new Memory Segment containing 65536 bytes
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="size"></param>
        void AddSegment(ushort segmentNumber, int size = 0x10000);

        /// <summary>
        ///     Adds a Decompiled code segment 
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <param name="segmentInstructionList"></param>
        void AddSegment(ushort segmentNumber, InstructionList segmentInstructionList);
        
        /// <summary>
        ///     Directly adds a raw segment from an NE file segment
        /// </summary>
        /// <param name="segment"></param>
        void AddSegment(Segment segment);

        /// <summary>
        ///     Returns the Segment information for the desired Segment Number
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <returns></returns>
        Segment GetSegment(ushort segmentNumber);

        /// <summary>
        ///     Verifies the specified segment is defined
        /// </summary>
        /// <param name="segmentNumber"></param>
        /// <returns></returns>
        bool HasSegment(ushort segmentNumber);

        //*** Getters ***

        /// <summary>
        ///     Returns the decompiled instruction from the specified segment:pointer
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="instructionPointer"></param>
        /// <returns></returns>
        Instruction GetInstruction(ushort segment, ushort instructionPointer);
        
        /// <summary>
        ///     Returns a single byte from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        byte GetByte(IntPtr16 pointer);

        /// <summary>
        ///     Returns a single byte from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        byte GetByte(ushort segment, ushort offset);

        /// <summary>
        ///     Returns an unsigned word from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        ushort GetWord(IntPtr16 pointer);

        /// <summary>
        ///     Returns an unsigned word from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        ushort GetWord(ushort segment, ushort offset);

        /// <summary>
        ///     Returns an unsigned byte from the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        ushort GetWord(string variableName);

        /// <summary>
        ///     Returns a pointer stored at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        IntPtr16 GetPointer(IntPtr16 pointer);

        /// <summary>
        ///     Returns a pointer stored at the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        IntPtr16 GetPointer(ushort segment, ushort offset);

        /// <summary>
        ///     Returns a pointer stored at the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        IntPtr16 GetPointer(string variableName);

        /// <summary>
        ///     Returns an array with desired count from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetArray(IntPtr16 pointer, ushort count);

        /// <summary>
        ///     Returns an array with the desired count from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetArray(ushort segment, ushort offset, ushort count);

        /// <summary>
        ///     Returns an array with the desired count from the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetArray(string variableName, ushort count);

        /// <summary>
        ///     Returns an array containing the cstring stored at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetString(IntPtr16 pointer, bool stripNull = false);

        /// <summary>
        ///     Returns an array containing the cstring stored at the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetString(ushort segment, ushort offset, bool stripNull = false);

        /// <summary>
        ///     Returns an array containing cstring stored at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="stripNull"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetString(string variableName, bool stripNull = false);

        //*** Setters ***

        /// <summary>
        ///     Sets the specified byte at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        void SetByte(IntPtr16 pointer, byte value);

        /// <summary>
        ///     Sets the specified byte at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        void SetByte(ushort segment, ushort offset, byte value);

        /// <summary>
        ///     Sets the specified byte at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        void SetByte(string variableName, byte value);

        /// <summary>
        ///     Sets the specified word at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        void SetWord(IntPtr16 pointer, ushort value);

        /// <summary>
        ///     Sets the specified word at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        void SetWord(ushort segment, ushort offset, ushort value);

        /// <summary>
        ///     Sets the specified word at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        void SetWord(string variableName, ushort value);

        /// <summary>
        ///     Sets the specified array at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="array"></param>
        void SetArray(IntPtr16 pointer, ReadOnlySpan<byte> array);

        /// <summary>
        ///     Sets the specified array at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="array"></param>
        void SetArray(ushort segment, ushort offset, ReadOnlySpan<byte> array);

        /// <summary>
        ///     Sets the specified array at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="array"></param>
        void SetArray(string variableName, ReadOnlySpan<byte> array);

        /// <summary>
        ///     Sets the specified pointer value at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        void SetPointer(IntPtr16 pointer, IntPtr16 value);

        /// <summary>
        ///     Sets the specified pointer value at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        void SetPointer(string variableName, IntPtr16 value);

        /// <summary>
        ///     Sets the specified pointer value at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        void SetPointer(ushort segment, ushort offset, IntPtr16 value);

        /// <summary>
        ///     Zeroes out the memory at the specified pointer for the desired number of bytes
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="length"></param>
        void SetZero(IntPtr16 pointer, int length);

        /// <summary>
        ///     Deletes all defined Segments from Memory
        /// </summary>
        void Clear();
    }
}