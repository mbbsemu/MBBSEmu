using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;
using System;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Interface for Memory Management within MBBSEmu
    /// </summary>
    public interface IMemoryCore : IDisposable
    {
        //*** Variable Memory Management ***

        /// <summary>
        ///     Returns the pointer to a defined variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        FarPtr GetVariablePointer(string name);

        /// <summary>
        ///     Safe retrieval of a pointer to a defined variable
        ///
        ///     Returns false if the variable isn't defined
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pointer"></param>
        /// <returns></returns>
        bool TryGetVariablePointer(string name, out FarPtr pointer);

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
        FarPtr GetOrAllocateVariablePointer(string name, ushort size = 0x0, bool declarePointer = false);

        /// <summary>
        ///     Allocates the specified variable name with the desired size
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <param name="declarePointer"></param>
        /// <returns></returns>
        FarPtr AllocateVariable(string name, ushort size, bool declarePointer = false);

        /// <summary>
        ///     Allocates the specific number of Big Memory Blocks with the desired size
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        FarPtr AllocateBigMemoryBlock(ushort quantity, ushort size);

        /// <summary>
        ///     Returns the specified block by index in the desired memory block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        FarPtr GetBigMemoryBlock(FarPtr block, ushort index);

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
        ///     Recompiles the memory located at segment:instructionPointer and returns the instruction.
        /// </summary>
        Instruction Recompile(ushort segment, ushort instructionPointer);

        /// <summary>
        ///     Returns a single byte from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        byte GetByte(FarPtr pointer);

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
        ushort GetWord(FarPtr pointer);

        /// <summary>
        ///     Returns an unsigned word from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        ushort GetWord(ushort segment, ushort offset);

        /// <summary>
        ///     Returns an unsigned word from the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        ushort GetWord(string variableName);

        /// <summary>
        ///     Returns an unsigned double word from the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        uint GetDWord(string variableName);

        /// <summary>
        ///     Returns an unsigned double word from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        uint GetDWord(FarPtr pointer);

        /// <summary>
        ///     Returns an unsigned double word from the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        uint GetDWord(ushort segment, ushort offset);

        /// <summary>
        ///     Returns a pointer stored at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        FarPtr GetPointer(FarPtr pointer);

        /// <summary>
        ///     Returns a pointer stored at the specified segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        FarPtr GetPointer(ushort segment, ushort offset);

        /// <summary>
        ///     Returns a pointer stored at the specified defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        FarPtr GetPointer(string variableName);

        /// <summary>
        ///     Returns an array with desired count from the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        ReadOnlySpan<byte> GetArray(FarPtr pointer, ushort count);

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
        ReadOnlySpan<byte> GetString(FarPtr pointer, bool stripNull = false);

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
        void SetByte(FarPtr pointer, byte value);

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
        void SetWord(FarPtr pointer, ushort value);

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
        ///     Sets the specified double word at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        void SetDWord(FarPtr pointer, uint value);

        /// <summary>
        ///     Sets the specified double word at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        void SetDWord(ushort segment, ushort offset, uint value);

        /// <summary>
        ///     Sets the specified double word at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        void SetDWord(string variableName, uint value);

        /// <summary>
        ///     Sets the specified array at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="array"></param>
        void SetArray(FarPtr pointer, ReadOnlySpan<byte> array);

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
        ///     Writes the specified byte the specified number of times starting at the specified pointer
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        void FillArray(ushort segment, ushort offset, ushort count, byte value);

        /// <summary>
        ///     Writes the specified byte the specified number of times starting at the specified pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        void FillArray(FarPtr pointer, ushort count, byte value);

        /// <summary>
        ///     Writes the specified byte the specified number of times starting at the specified variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="count"></param>
        /// <param name="value"></param>
        void FillArray(string variableName, ushort count, byte value);

        /// <summary>
        ///     Sets the specified pointer value at the desired pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="value"></param>
        void SetPointer(FarPtr pointer, FarPtr value);

        /// <summary>
        ///     Sets the specified pointer value at the defined variable
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="value"></param>
        void SetPointer(string variableName, FarPtr value);

        /// <summary>
        ///     Sets the specified pointer value at the desired segment:offset
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        void SetPointer(ushort segment, ushort offset, FarPtr value);

        /// <summary>
        ///     Zeroes out the memory at the specified pointer for the desired number of bytes
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="length"></param>
        void SetZero(FarPtr pointer, int length);

        /// <summary>
        ///     Deletes all defined Segments from Memory
        /// </summary>
        void Clear();

        /// <summary>
        ///     Returns a newly allocated Segment in "Real Mode" memory
        /// </summary>
        /// <returns></returns>
        FarPtr AllocateRealModeSegment(ushort segmentSize = ushort.MaxValue);

    }
}
