using MBBSEmu.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.Memory
{
    /// <summary>
    ///   A Memory Allocator that implements malloc/free.
    /// </summary>
    public class MemoryAllocator
  {
    /// <summary>
    ///   Represents a block of memory.
    /// </summary>
    private class MemoryBlock
    {
      /// <summary>
      ///   Offset within the segment where this memory begins.
      /// </summary>
      public uint Offset { get; set; }
      /// <summary>
      ///   Size in bytes of this memory block.
      /// </summary>
      public uint Size { get; set; }

      /// <summary>
      ///   Offset within the segment where this memory ends.
      /// </summary>
      public uint EndOffset { get => Offset + Size; }
    }

    /// <summary>
    ///   List of free memory blocks we currently have.
    /// </summary>
    private readonly LinkedList<MemoryBlock> _freeBlocks = new();
    /// <summary>
    ///   Stores all of our allocated memory blocks.
    ///
    ///   Key is the pointer, Value is the size of the allocated memory block.
    /// </summary>
    private readonly ConcurrentDictionary<FarPtr, uint> _allocatedBlocks = new();

    /// <summary>
    ///   Lowest memory address that this allocator can return as a valid pointer.
    /// </summary>
    public FarPtr BasePointer { get; init; }

    /// <summary>
    ///   Total capacity in bytes that this memory allocator can allocate.
    ///
    ///   Could allocate less than this value depending on the fragmentation level.
    /// </summary>
    public uint Capacity { get; init; }

    /// <summary>
    ///   How many remaining bytes are available to be allocated.
    /// </summary>
    public uint RemainingBytes { get; set; }

    public IMessageLogger Logger { get; init; }

    /// <summary>
    ///   The number of free blocks being tracked.
    ///
    ///   Only used for testing.
    /// </summary>
    public int FreeBlocks => _freeBlocks.Count;

    /// <summary>
    ///   Alignment to use when returning pointers. Should be a power of 2.
    /// </summary>
    public ushort Alignment { get; init; }

    /// <summary>
    ///   Constructs a new MemoryAllocator.
    ///
    ///   basePointer + capacity must be smaller than a segment size, to ensure it will not overflow
    ///   allocations into the next segment.
    /// </summary>
    /// <param name="logger">logger</param>
    /// <param name="basePointer">lowest memory address returnable from malloc</param>
    /// <param name="capacity">size in bytes that this allocator can draw memory from</param>
    /// <param name="alignment">alignment in bytes for returned pointers, power of 2</param>
    public MemoryAllocator(IMessageLogger logger, FarPtr basePointer, uint capacity, ushort alignment)
    {
      if (alignment == 0)
        throw new ArgumentException("Alignment must be non-zero");
      if (!basePointer.IsAligned(alignment))
        throw new ArgumentException($"basePointer must be aligned on a boundary of {alignment}");

      var endOffset = basePointer.Offset + capacity;
      if (endOffset > 0x10000)
        throw new ArgumentException("pointer + size overflows segment");

      BasePointer = basePointer;
      Capacity = capacity;
      RemainingBytes = capacity;
      Logger = logger;
      Alignment = alignment;

      _freeBlocks.AddFirst(new MemoryBlock() { Offset = basePointer.Offset, Size = capacity });
    }

    /// <summary>
    ///   Allocates a block of memory of size size.
    ///
    ///   Returns NULL if no memory is available.
    /// </summary>
    /// <param name="size">size in bytes to allocate.</param>
    /// <returns></returns>
    public FarPtr Malloc(uint size)
    {
      if (_freeBlocks.Count == 0)
      {
        Logger?.Warn($"Failed to allocate memory of size {size} since we have no free blocks, or bad argument.");
        return FarPtr.Empty;
      }

      // align
      size = size == 0 ? Alignment : (uint)((size + Alignment - 1) & ~(Alignment - 1));

      var foundBlock = _freeBlocks.EnumerateNodes()
          .Where(memoryBlock => memoryBlock.Value.Size >= size)
          .Aggregate((LinkedListNode<MemoryBlock>)null, (curMin, memoryBlock) => (curMin == null || (memoryBlock.Value.Size < curMin.Value.Size) ? memoryBlock : curMin));
      if (foundBlock == null)
      {
        Logger?.Warn($"Failed to allocate memory of size {size} since we can't find a large enough free block.");
        return FarPtr.Empty;
      }

      var returnPtr = new FarPtr(BasePointer.Segment, (ushort)foundBlock.Value.Offset);
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

      RemainingBytes -= size;
      return returnPtr;
    }

    /// <summary>
    ///   Frees a block of memory previously allocated with Malloc.
    /// </summary>
    /// <param name="ptr">block of memory previous allocated with Malloc</param>
    public void Free(FarPtr ptr)
    {
      if (ptr == FarPtr.Empty)
        return;

      if (!_allocatedBlocks.TryRemove(ptr, out var size))
      {
        Logger?.Error($"Attempting to deallocate invalid memory block at {ptr}");
        return;
      }

      RemainingBytes += size;

      // see if we can remerge
      var node = _freeBlocks.EnumerateNodes().FirstOrDefault(node => node.Value.Offset == ptr.Offset + size || node.Value.EndOffset == ptr.Offset);
      if (node != null)
      {
        // if the adjacent free block is immediately after the freed memory, change the adjacent
        // free block starting offset (lowers it)
        if (node.Value.Offset == ptr.Offset + size)
          node.Value.Offset = ptr.Offset;

        node.Value.Size += size;
        Compact(node);
        return;
      }

      _freeBlocks.AddFirst(new MemoryBlock() { Offset = ptr.Offset, Size = size });
    }

    public int GetAllocatedMemorySize(FarPtr ptr)
    {
      if (!_allocatedBlocks.TryGetValue(ptr, out var size))
        return -1;

      return (int)size;
    }

    private LinkedListNode<MemoryBlock> MergeNodesAndRemoveDeadNode(LinkedListNode<MemoryBlock> liveNode, LinkedListNode<MemoryBlock> deadNode)
    {
      liveNode.Value.Size += deadNode.Value.Size;
      _freeBlocks.Remove(deadNode);
      return liveNode;
    }

    /// <summary>
    ///   Compacts _freeBlocks as best we can.
    /// </summary>
    /// <param name="updatedBlock">the memory block that caused the compaction</param>
    private void Compact(LinkedListNode<MemoryBlock> updatedBlock)
    {
      while (_freeBlocks.Count > 1)
      {
        // look for nodes that join each other
        var node = _freeBlocks.EnumerateNodes().FirstOrDefault(node => node.Value.EndOffset == updatedBlock.Value.Offset || updatedBlock.Value.EndOffset == node.Value.Offset);
        if (node == null)
          return;

        LinkedListNode<MemoryBlock> liveNode;
        LinkedListNode<MemoryBlock> deadNode;
        // live node always has the lowest offset
        if (node.Value.Offset < updatedBlock.Value.Offset)
        {
          liveNode = node;
          deadNode = updatedBlock;
        }
        else
        {
          liveNode = updatedBlock;
          deadNode = node;
        }
        updatedBlock = MergeNodesAndRemoveDeadNode(liveNode, deadNode);
        // we've updated another block, so try to compact again
        continue;
      }
    }
  }
}
