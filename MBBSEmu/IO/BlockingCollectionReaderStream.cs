using System.Collections.Concurrent;
using System;

namespace MBBSEmu.IO
{
    public class BlockingCollectionReaderStream : IStream
    {
        private readonly BlockingCollection<byte> _collection;

        public BlockingCollectionReaderStream(BlockingCollection<byte> collection)
        {
            _collection = collection;
        }

        public byte Read() => _collection.Take();

        public void Write(byte c) => throw new NotSupportedException("Can't write to BlockingCollectionReaderStream");

        public void Write(byte[] c) => throw new NotSupportedException("Can't write to BlockingCollectionReaderStream");

        public void Flush() {}

        public void Dispose() {}
    }
}
