using System.Collections.Concurrent;
using System;

namespace MBBSEmu.IO
{
    public class BlockingCollectionWriterStream : IStream
    {
        private readonly BlockingCollection<byte[]> _collection;

        public BlockingCollectionWriterStream(BlockingCollection<byte[]> collection)
        {
            _collection = collection;
        }

        public int ReadByte() => throw new NotSupportedException("Can't write to BlockingCollectionReaderStream");

        public void Write(byte c) => _collection.Add(new byte[] {c});

        public void Write(byte[] c) => _collection.Add(c);

        public void Flush() {}

        public void Dispose() {}
    }
}
