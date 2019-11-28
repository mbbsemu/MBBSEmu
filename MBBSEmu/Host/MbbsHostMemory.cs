using System;

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

        public int AllocateHostMemory(int size)
        {
            var currentPointer = _hostMemoryPointer;
            _hostMemoryPointer += size;
            return currentPointer;
        }
    }
}
