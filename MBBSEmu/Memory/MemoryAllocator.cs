using NLog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace MBBSEmu.Memory
{
  public class MemoryAllocator
  {
    private class MemoryBlock
    {
      public ushort Offset { get; set; }
      public uint Size { get; set; }
    }

    private LinkedList<MemoryBlock> _freeBlocks = new();
    private ConcurrentDictionary<FarPtr, uint> _allocatedBlocks = new();

    public FarPtr BasePointer { get; init; }

    public uint Size { get; init; }

    public ILogger Logger { get; init; }

    public int FreeBlocks { get => _freeBlocks.Count; }

    public MemoryAllocator(ILogger logger, FarPtr basePointer, uint size)
    {
      var endOffset = basePointer.Offset + size;
      if (endOffset > 0x10000)
        throw new ArgumentException("pointer + size overflows segment");

      BasePointer = basePointer;
      Size = size;

      _freeBlocks.AddFirst(new MemoryBlock() { Offset = basePointer.Offset, Size = size });
    }

    public FarPtr Malloc(ushort size)
    {
      if (size == 0)
        return FarPtr.Empty;

      var foundBlock = _freeBlocks.EnumerateNodes()
          .Where(memoryBlock => memoryBlock.Value.Size >= size)
          .Aggregate((curMin, memoryBlock) => (curMin == null || (memoryBlock.Value.Size < curMin.Value.Size) ? memoryBlock : curMin));
      if (foundBlock == null)
        return FarPtr.Empty;

      var returnPtr = new FarPtr(BasePointer.Segment, foundBlock.Value.Offset);
      _allocatedBlocks[returnPtr] = size;

      if (foundBlock.Value.Size > size) // break it up
      {
        foundBlock.Value.Offset += size;
        foundBlock.Value.Size -= size;
      }
      else // exactly the same size, so not a free block anymore
      {
        _freeBlocks.Remove(foundBlock);
      }

      return returnPtr;
    }

    public void Free(FarPtr ptr)
    {
      if (ptr == FarPtr.Empty)
        return;

      if (!_allocatedBlocks.TryRemove(ptr, out var size))
      {
        Logger?.Warn($"Attempting to deallocate invalid memory block at {ptr}");
        return;
      }

      // see if we can remerge
      var node = _freeBlocks.EnumerateNodes().Where(node => node.Value.Offset == ptr.Offset + size).FirstOrDefault();
      if (node != null)
      {
        node.Value.Offset -= (ushort)size;
        node.Value.Size += size;
        return;
      }

      node = _freeBlocks.EnumerateNodes().Where(node => node.Value.Offset == ptr.Offset - size).FirstOrDefault();
      if (node != null)
      {
        //node.Value.Offset += (ushort)size;
        node.Value.Size += size;
        return;
      }

      _freeBlocks.AddFirst(new MemoryBlock() { Offset = ptr.Offset, Size = size});
    }
  }
}
