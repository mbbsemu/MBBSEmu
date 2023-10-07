using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MBBSEmu.Logging.Targets
{

    public class QueueTarget : ILoggingTarget, IQueueTarget
    {
        /// <summary>
        ///     Maximum number of items to queue before dropping
        /// </summary>
        private readonly int _maxQueueSize;

        private readonly ConcurrentQueue<object[]> _queue;

        public QueueTarget(int maxQueueSize = 1000)
        {
            _queue = new ConcurrentQueue<object[]>();
            _maxQueueSize = maxQueueSize;
        }

        /// <summary>
        ///     ILoggingTarget Implementation
        /// </summary>
        /// <param name="logEntry">params object[] consisting of (logLevel, logMessage)</param>
        public void Write(params object[] logEntry)
        {
            //Use reflection to get the name of the class calling this method
            //TODO: We should move this reflection up the stack to the log factory in GetLogger<T>() and pass it into the logger so we don't have to do this every time
            var callingType = new System.Diagnostics.StackTrace().GetFrame(3)?.GetMethod()?.DeclaringType;

            //Append new Values to the beginning of the array
            var objectToWrite = new object[logEntry.Length + 2];
            objectToWrite[0] = DateTime.Now;
            objectToWrite[1] = callingType;
            Array.Copy(logEntry, 0, objectToWrite, 2, logEntry.Length);

            //Check Queue Length
            if (_queue.Count <= _maxQueueSize)
                _queue.Enqueue(objectToWrite);
        }

        public object[] Dequeue() => _queue.TryDequeue(out var queueItem) ? queueItem : null;

        public IList<object[]> DequeueAll()
        {
            var queueItems = new List<object[]>();

            while (_queue.TryDequeue(out var queueItem))
            {
                queueItems.Add(queueItem);
            }

            return queueItems;
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}
