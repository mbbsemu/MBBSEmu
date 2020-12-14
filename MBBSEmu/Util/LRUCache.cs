using System.Collections.Concurrent;
using System.Collections.Generic;
using System;

namespace MBBSEmu.Util
{
  public class LRUCache<TKey, TValue> : IDictionary<TKey,TValue>
  {
    private class Data
    {
      public Data(TKey key, TValue data)
      {
        this.data = data;
        this.queueNode = new LinkedListNode<TKey>(key);
      }

      public TValue data;
      public readonly LinkedListNode<TKey> queueNode;
    }

    private readonly ConcurrentDictionary<TKey, Data> _data = new();
    private readonly LinkedList<TKey> _list = new();

    public int MaxSize { get; init; }

    public LRUCache(int maxSize)
    {
      if (maxSize <= 0)
        throw new ArgumentException("LRUCache needs to have size > 0");

      MaxSize = maxSize;
    }

    private Data InsertNewItem(TKey key, TValue value)
    {
      if (Count >= MaxSize)
      {
        // purge an item
        _data.Remove(_list.Last.Value, out _);
        _list.RemoveLast();
      }

      return new Data(key, value);
    }

    public TValue this[TKey key]
    {
      get
      {
        var data = _data[key];
        _list.Remove(data.queueNode);
        _list.AddFirst(data.queueNode);
        return data.data;
      }
      set
      {
        var newValue = _data.AddOrUpdate(key, key => InsertNewItem(key, value), (key, oldValue) => {
          if (oldValue != null)
            _list.Remove(oldValue.queueNode);

          oldValue.data = value;
          return oldValue;
        });

        _list.AddFirst(newValue.queueNode);
      }
    }

    public int Count { get => _data.Count; }

    public int ListCount { get => _list.Count; }
    public TKey MostRecentlyUsed { get => _list.First.Value; }

    public bool IsReadOnly { get => false; }

    public System.Collections.Generic.ICollection<TKey> Keys { get => _data.Keys; }

    public System.Collections.Generic.ICollection<TValue> Values { get => throw new NotSupportedException(); }

    public void Add(KeyValuePair<TKey,TValue> item) => this[item.Key] = item.Value;

    public void Clear() => _data.Clear();

    public bool Contains(KeyValuePair<TKey, TValue> item) => _data.ContainsKey(item.Key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotSupportedException();

    public bool Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

    public void Add(TKey key, TValue value) => this[key] = value;

    public bool ContainsKey(TKey key) => _data.ContainsKey(key);

    public bool Remove(TKey key) => throw new NotSupportedException();

    public bool TryGetValue(TKey key, out TValue value)
    {
      var ret = _data.TryGetValue(key, out var v);
      value = v.data;
      return ret;
    }

    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => throw new NotSupportedException();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();
  }
}
