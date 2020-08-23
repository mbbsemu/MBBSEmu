using System;
using System.Collections.Generic;

namespace MBBSEmu.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>Returns an IEnumerable<int> of all indices inside items that match predicate
        /// </summary>
        /// <param name="items">IEnumerable of your items</param>
        /// <param name="length">How many items to enumerate out of items (can be less than the
        ///    total size of the IEnumerable passed in).
        /// </param>
        /// <param name="predicate">Predicate used to indicate a matching item</param>
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
