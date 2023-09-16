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
        private int _maxQueueSize;

        private readonly ConcurrentQueue<string> _queue;

        public QueueTarget(int maxQueueSize = 1000)
        {
            _queue = new ConcurrentQueue<string>();
            _maxQueueSize = maxQueueSize;
        }

        /// <summary>
        ///     ILoggingTarget Implementation
        /// </summary>
        /// <param name="logEntry"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Write(params object[] logEntry)
        {
            if (logEntry.Length != 2)
                return;

            var logMessage = (string)logEntry[0];
            var logLevel = ((EnumLogLevel)logEntry[1]).ToString();

            //Use reflection to get the name of the class calling this method
            var callingType = new System.Diagnostics.StackTrace().GetFrame(3)?.GetMethod()?.DeclaringType;

            //Check Queue Length against the Max Queue Size, if so, remove the last item entered into the queue and enqueue the new item stating the queue is full
            if (_queue.Count >= _maxQueueSize)
            {
                _queue.TryDequeue(out _);
                _queue.Enqueue($"Queue is full, dropping log entry from {callingType} at {DateTime.Now} with message: {logMessage}");
            }

            //Enqueue the new item
            _queue.Enqueue($"[{DateTime.Now:O}]\t{callingType}\t[{logLevel}]\t{logMessage}");
        }

        public string Dequeue()
        {
            if (_queue.Count > 0)
            {
                if(_queue.TryDequeue(out var queueItem))
                    return queueItem;
                else
                    return null;
            }

            return null;
        }

        public IList<string> DequeueAll()
        {
            var queueItems = new List<string>();

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
