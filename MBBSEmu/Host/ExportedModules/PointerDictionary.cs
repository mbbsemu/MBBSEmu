using System.Collections.Generic;

namespace MBBSEmu.Host.ExportedModules
{
    public class PointerDictionary<TValue> : Dictionary<int, TValue>
    {
        public PointerDictionary() : base()
        {
        }

        public PointerDictionary(int capacity) : base(capacity)
        {
        }

        public int GetPointer()
        {
            for (var i = 0; i < int.MaxValue; i++)
            {
                if (!this.ContainsKey(i))
                    return i;
            }

            return -1;
        }

        /// <summary>
        ///     Allocates a new record for the specified value at an unused pointer
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int Allocate(TValue value)
        {
            var newPointer = GetPointer();
            this[newPointer] = value;
            return newPointer;
        }
    }
}
