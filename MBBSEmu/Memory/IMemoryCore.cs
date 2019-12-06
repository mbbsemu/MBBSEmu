using Iced.Intel;
using MBBSEmu.Disassembler.Artifacts;

namespace MBBSEmu.Memory
{
    public interface IMemoryCore
    {
        /// <summary>
        ///     Declares a new 16-bit Segment and allocates it to the defined Segment Number
        /// </summary>
        /// <param name="segmentNumber"></param>
        void AddSegment(ushort segmentNumber);

        void AddSegment(Segment segment);
        Segment GetSegment(ushort segmentNumber);
        bool HasSegment(ushort segmentNumber);

        Instruction GetInstruction(ushort segment, int instructionPointer);
        byte GetByte(ushort segment, ushort offset);
        ushort GetWord(ushort segment, ushort offset);
        byte[] GetArray(ushort segment, ushort offset, ushort count);

        /// <summary>
        ///     Reads an array of bytes from the specified segment:offset, stopping
        ///     at the first null character denoting the end of the string.
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        byte[] GetString(ushort segment, ushort offset);

        void SetByte(ushort segment, ushort offset, byte value);
        void SetWord(ushort segment, ushort offset, ushort value);
        void SetArray(ushort segment, ushort offset, byte[] array);
    }
}