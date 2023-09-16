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

        private readonly ConcurrentQueue<object[]> _queue;

        public QueueTarget(int maxQueueSize = 1000)
        {
            _queue = new ConcurrentQueue<object[]>();
            _maxQueueSize = maxQueueSize;
        }

        /// <summary>
        ///     ILoggingTarget Implementation
        /// </summary>
        /// <param name="logEntry"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Write(params object[] logEntry)
        {
            //Check Queue Length
            if (_queue.Count <= _maxQueueSize)
                _queue.Enqueue(logEntry);
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
