using System.Collections.Generic;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     A PointerDictionary is essentially a Dictionary with some helper methods to automatically
    ///     allocate new entries in the Base dictionary by the lowest available "pointer".
    ///
    ///     This means we always have a consistent, solid block of dictionary values
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class PointerDictionary<TValue> : Dictionary<int, TValue>
    {
        public PointerDictionary() : base()
        {
        }

        public PointerDictionary(int capacity) : base(capacity)
        {
        }

        /// <summary>
        ///     Allocates a new record for the specified value at an available pointer
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int Allocate(TValue value)
        {
            var newPointer = GetPointer();
            this[newPointer] = value;
            return newPointer;
        }

        /// <summary>
        ///     Finds the lowest available pointer
        /// </summary>
        /// <returns></returns>
        private int GetPointer()
        {
            for (var i = 0; i < int.MaxValue; i++)
            {
                if (!ContainsKey(i))
                    return i;
            }

            return -1;
        }
    }
}
