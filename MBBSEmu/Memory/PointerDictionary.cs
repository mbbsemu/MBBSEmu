using System;
using System.Collections.Generic;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     A PointerDictionary is essentially a Dictionary with some helper methods to automatically
    ///     allocate new entries in the Base dictionary by the lowest available "pointer".
    ///
    ///     This means we always have a consistent, mostly solid block of dictionary values as
    ///     key values that are removed are reused.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class PointerDictionary<TValue> : Dictionary<int, TValue>
    {
        private readonly int _minimumValue;
        private readonly int _maximumValue;

        public PointerDictionary(int minimumValue = 0, int maximumValue = int.MaxValue) : base()
        {
            _minimumValue = minimumValue;
            _maximumValue = maximumValue;
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
            for (var i = _minimumValue; i < _maximumValue; i++)
            {
                if (!ContainsKey(i))
                    return i;
            }
            throw new Exception("Pointer Dictionary is Full");
        }
    }
}
