using System;
using System.Collections.Generic;

namespace MBBSEmu.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<int> FindIndexes<T>(this IEnumerable<T> items, int length, Func<T, bool> predicate)
        {
            int index = 0;
            foreach (T item in items)
            {
                if (index >= length)
                {
                  break;
                }

                if (predicate(item))
                {
                    yield return index;
                }

                ++index;
            }
        }
    }
}
