using System;
using System.Collections.Generic;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///     Cache for objects to be shared across domains
    /// </summary>
    public class GlobalCache : IGlobalCache
    {
        private readonly Dictionary<string, object> _cacheDictionary;

        public GlobalCache()
        {
            _cacheDictionary = new Dictionary<string, object>();
        }

        public T Get<T>(string key)
        {
            if (!_cacheDictionary.TryGetValue(key, out var result))
                throw new Exception($"Key not found in Cache: {key}");

            return (T) result;
        }

        public object Get(string key) => Get<object>(key);

        public bool Set(string key, object value)
        {
            if (_cacheDictionary.ContainsKey(key))
            {
                _cacheDictionary[key] = value;
            }
            else
            {
                _cacheDictionary.Add(key, value);
            }

            return true;
        }
    }
}
