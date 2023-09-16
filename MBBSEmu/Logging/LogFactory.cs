using System;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    public class LogFactory
    {
        private static readonly Dictionary<Type, object> Loggers = new();

        public LogFactory() { }

        public void AddLogger<T>(T logger)
        {
            //If the logger already exists, overwrite the existing logger
            if (Loggers.ContainsKey(typeof(T)))
                Loggers.Remove(typeof(T));

            Loggers.Add(typeof(T), logger);
        }


        public T GetLogger<T>()
        {
            if (Loggers.ContainsKey(typeof(T)))
                return (T)Loggers[typeof(T)];

            var logger = (T)Activator.CreateInstance(typeof(T), null);
            Loggers.Add(typeof(T), logger);

            return logger;
        }
    }
}
