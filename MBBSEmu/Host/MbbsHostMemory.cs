using System;
using System.Collections.Generic;

namespace MBBSEmu.Host
{
    /// <summary>
    ///     MBBS Host Memory Controller
    ///
    ///     This class represents the memory space within the MajorBBS/Worldgroup Host Process
    /// </summary>
    public class MbbsHostMemory
    {
        /// <summary>
        ///     Host Process Memory Space
        /// </summary>
        private readonly byte[] _hostMemorySpace;

        /// <summary>
        ///     As memory is allocated, this will be incremented
        /// </summary>
        private int _hostMemoryPointer = 0x0;

        public MbbsHostMemory()
        {
            _hostMemorySpace = new byte[0x800000];
        }

        public int GetHostByte(int offset) => _hostMemorySpace[offset];
        public int GetHostWord(int offset) => BitConverter.ToUInt16(_hostMemorySpace, offset);
        public void IncrementHostPointer(int offset = 1) => _hostMemoryPointer += offset;
        public int GetHostPointer() => _hostMemoryPointer;
        public void SetHostByte(int offset, byte value) => _hostMemorySpace[offset] = value;
        public void SetHostWord(int offset, ushort value) => Array.Copy(BitConverter.GetBytes(value), 0, _hostMemorySpace, offset, 2);

        public void SetHostArray(int offset, byte[] array) => Array.Copy(array, 0, _hostMemorySpace, offset, array.Length);

        /// <summary>
        ///     Reads an array of bytes from the specified segment:offset, stopping
        ///     at the first null character denoting the end of the string.
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public byte[] GetString(int segment, int offset)
        {
            var output = new List<byte>();
            for (var i = 0; i < ushort.MaxValue; i++)
            {
                var inputByte = _hostMemorySpace[offset + 1];
                output.Add(inputByte);
                if (inputByte == 0)
                    break;
            }

            return output.ToArray();
        }


        public int AllocateHostMemory(int size)
        {
            var currentPointer = _hostMemoryPointer;
            _hostMemoryPointer += size;
            return currentPointer;
        }
    }
}
