using System;
using System.Collections.Generic;

namespace MBBSEmu.UI.Logging
{
    /// <summary>
    ///     Fixed-capacity log that drops its oldest entries once full, so the Admin Console's memory use
    ///     stays flat no matter how long the BBS runs
    /// </summary>
    public sealed class BoundedLog<T>
    {
        private readonly T[] _items;

        /// <summary>
        ///     Index in _items of the oldest retained entry
        /// </summary>
        private int _start;

        public BoundedLog(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _items = new T[capacity];
        }

        public int Capacity => _items.Length;

        public int Count { get; private set; }

        /// <summary>
        ///     Entry by age, where 0 is the oldest retained entry
        /// </summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _items[(_start + index) % _items.Length];
            }
        }

        /// <summary>
        ///     Appends the items in order, dropping the oldest entries beyond Capacity
        /// </summary>
        /// <returns>Number of entries dropped, counting new items that never fit</returns>
        public int Append(IReadOnlyList<T> items)
        {
            var dropped = 0;
            foreach (var item in items)
            {
                if (Count < _items.Length)
                {
                    _items[(_start + Count) % _items.Length] = item;
                    Count++;
                }
                else
                {
                    //Full: overwrite the oldest entry, which makes the next one the oldest
                    _items[_start] = item;
                    _start = (_start + 1) % _items.Length;
                    dropped++;
                }
            }

            return dropped;
        }
    }
}
