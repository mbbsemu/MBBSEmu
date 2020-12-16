using System.Collections.Concurrent;
using System.Collections.Generic;
using System;

namespace MBBSEmu.Util
{
  /// <summary>
  ///   A Least Recently Used cache. Stores data up to MaxSize elements, and any attempts to add
  ///   new items will purge the least recently used/accessed item.
  /// </summary>
  /// <remarks>not thread safe</remarks>
  /// <typeparam name="TKey">key type</typeparam>
  /// <typeparam name="TValue">value type</typeparam>
  public class LRUCache<TKey, TValue> : IDictionary<TKey,TValue>
  {
    /// <summary>
    ///   Data stored inside the Dictionary.
    ///
    ///   <para/>Keeps track of the raw data as well as a reference to the LinkedListNode for
    ///   efficient removal/insertion (O(1) vs O(n)).
    /// </summary>
    private class Data
    {
      public Data(TKey key, TValue data)
      {
        _data = data;
        _recentlyUsedNode = new LinkedListNode<TKey>(key);
      }

      public TValue _data;
      public readonly LinkedListNode<TKey> _recentlyUsedNode;
    }

    /// <summary>
    ///   Holds all the data.
    /// </summary>
    private readonly ConcurrentDictionary<TKey, Data> _data = new();
    /// <summary>
    ///   The list used for keeping track of the most recently used items.
    ///
    ///   <para/>Front of the list is the most recently used, with the rear being the least.
    /// </summary>
    /// <returns></returns>
    private readonly LinkedList<TKey> _recentlyUsedList = new();

    /// <summary>
    ///   The maximum number of items this collection will hold.
    /// </summary>
    /// <value></value>
    public int MaxSize { get; init; }

    public LRUCache(int maxSize)
    {
      if (maxSize < 0)
        throw new ArgumentException("LRUCache needs to have size >= 0");

      MaxSize = maxSize;
    }

    private Data InsertNewItem(TKey key, TValue value, out bool shouldRemoveData, out LinkedListNode<TKey> nodeToRemove)
    {
      if (Count >= MaxSize)
      {
        shouldRemoveData = true;
        nodeToRemove = _recentlyUsedList.Last;
      }
      else
      {
        shouldRemoveData = false;
        nodeToRemove = null;
      }

      return new Data(key, value);
    }

    public TValue this[TKey key]
    {
      get
      {
        var data = _data[key];
        SetMostRecentlyUsed(data);
        return data._data;
      }
      set
      {
        if (MaxSize == 0)
          return;

        LinkedListNode<TKey> listItemToRemove = null;
        bool shouldRemoveData = false;
        var newValue = _data.AddOrUpdate(
          key,
          key => InsertNewItem(key, value, out shouldRemoveData, out listItemToRemove),
          (key, oldValue) =>
          {
            listItemToRemove = oldValue._recentlyUsedNode;
            oldValue._data = value;
            return oldValue;
          });

        if (shouldRemoveData)
          _data.Remove(listItemToRemove.Value, out _);
        if (listItemToRemove != null)
          _recentlyUsedList.Remove(listItemToRemove);

        _recentlyUsedList.AddFirst(newValue._recentlyUsedNode);
      }
    }

    /// <summary>
    ///   The number of items currently inside this cache.
    /// </summary>
    public int Count { get => _data.Count; }

    /// <summary>
    ///   The number of items tracked inside _recentlyUsedList. You probably want to use Count instead.
    ///
    ///   <para/>This is mostly a test-only property, don't rely on this value in real code.
    /// </summary>
    /// <value></value>
    public int ListCount { get => _recentlyUsedList.Count; }

    /// <summary>
    ///   The most recently used key.
    /// </summary>
    public TKey MostRecentlyUsed { get => _recentlyUsedList.First.Value; }

    public bool IsReadOnly { get => false; }

    public System.Collections.Generic.ICollection<TKey> Keys { get => _data.Keys; }

    public System.Collections.Generic.ICollection<TValue> Values { get => throw new NotSupportedException(); }

    public void Add(KeyValuePair<TKey,TValue> item) => this[item.Key] = item.Value;
    public void Add(TKey key, TValue value) => this[key] = value;

    public void Clear()
    {
      _data.Clear();
      _recentlyUsedList.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
      => TryGetValue(item.Key, out var value) && value.Equals(item.Value);

    public bool ContainsKey(TKey key) => _data.ContainsKey(key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotSupportedException();

    public bool Remove(KeyValuePair<TKey, TValue> item)
      => Contains(item) && Remove(item.Key);

    public bool Remove(TKey key)
    {
      var ret = _data.Remove(key, out var v);
      if (v != null)
        _recentlyUsedList.Remove(v._recentlyUsedNode);

      return ret;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
      var ret = _data.TryGetValue(key, out var v);
      if (ret)
      {
        SetMostRecentlyUsed(v);

        value = v._data;
      }
      else
      {
        value = default(TValue);
      }
      return ret;
    }

    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => throw new NotSupportedException();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();

    private void SetMostRecentlyUsed(Data data)
    {
      _recentlyUsedList.Remove(data._recentlyUsedNode);
      _recentlyUsedList.AddFirst(data._recentlyUsedNode);
    }
  }
}
