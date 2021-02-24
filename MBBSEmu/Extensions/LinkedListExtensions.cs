using System.Collections.Generic;

public static class LinkedListExtensions
{
    public static IEnumerable<LinkedListNode<T>> EnumerateNodes<T>(this LinkedList<T> list)
    {
        var node = list.First;
        while(node != null)
        {
            yield return node;
            node = node.Next;
        }
    }
}
