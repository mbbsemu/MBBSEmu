using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    public class LogFactory
    {
        private static readonly ConcurrentDictionary<Type, object> Loggers = new();

        public LogFactory() { }

        public void AddLogger<T>(T logger)
        {
            //If the logger already exists, overwrite the existing logger
            if (Loggers.ContainsKey(typeof(T)))
            {
                Loggers.TryUpdate(typeof(T), logger, Loggers[typeof(T)]);
            }
            else
            {
                Loggers.TryAdd(typeof(T), logger);
            }
        }

        public T GetLogger<T>()
        {
            if (!Loggers.ContainsKey(typeof(T))) 
                throw new KeyNotFoundException($"Logger of type {typeof(T)} not found");

            return (T)Loggers[typeof(T)];
        }
    }
}
