using FluentAssertions;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class MemoryAllocator_Tests : TestBase
  {
    private const int DEFAULT_ALIGNMENT = 2;
    private const int SEGMENT = 1;

    private readonly IMessageLogger _logger = new ServiceResolver().GetService<LogFactory>().GetLogger<MessageLogger>();

    [Fact]
    public void Properties()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000, DEFAULT_ALIGNMENT);
      allocator.BasePointer.Should().Be(new FarPtr(SEGMENT, 0));
      allocator.Capacity.Should().Be(0x10000);
      allocator.RemainingBytes.Should().Be(0x10000);
    }

    [Fact]
    public void ConstructsJustEnough()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0xFFFE), 2, DEFAULT_ALIGNMENT);
      allocator.Should().NotBeNull();
    }

    [Fact]
    public void ConstructOverflowsSegment()
    {
      Action act = () => new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0xFFFF), 2, DEFAULT_ALIGNMENT);
      act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstructNotAligned()
    {
      Action act = () => new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0xFFFF), 1, DEFAULT_ALIGNMENT);
      act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllocateZero()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000, DEFAULT_ALIGNMENT);

      var ptr = allocator.Malloc(0);
      ptr.Should().NotBe(FarPtr.Empty);
      allocator.RemainingBytes.Should().Be(0xFFFE);
      allocator.GetAllocatedMemorySize(ptr).Should().Be(DEFAULT_ALIGNMENT);
    }

    [Fact]
    public void FreeNull()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000, DEFAULT_ALIGNMENT);
      allocator.Free(FarPtr.Empty);
      allocator.GetAllocatedMemorySize(FarPtr.Empty).Should().Be(-1);
    }

    [Fact]
    public void SimpleAllocation()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(1);
      allocator.RemainingBytes.Should().Be(0xFFFE - 16);
      allocator.GetAllocatedMemorySize(memory).Should().Be(16);
    }

    [Fact]
    public void AllocateEntireBlock()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory = allocator.Malloc(0xFFFE);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(0);
      allocator.RemainingBytes.Should().Be(0);
      allocator.GetAllocatedMemorySize(memory).Should().Be(0xFFFE);

      // and free it to make sure everything is back in place
      allocator.Free(memory);
      allocator.FreeBlocks.Should().Be(1);
      allocator.RemainingBytes.Should().Be(0xFFFE);
      allocator.GetAllocatedMemorySize(memory).Should().Be(-1);
    }

    [Fact]
    public void DoubleAllocation()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(18);

      allocator.FreeBlocks.Should().Be(1);
      allocator.RemainingBytes.Should().Be(0xFFFE - 32);
    }

    [Fact]
    public void AllocationAndFreeRepeat()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.Free(memory);

      memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(1);
      allocator.RemainingBytes.Should().Be(0xFFFE - 16);
    }

    [Fact]
    public void AllocationAndFreeOutOfOrderRepeatFirstOrder()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory1 = allocator.Malloc(16);
      memory1.Segment.Should().Be(SEGMENT);
      memory1.Offset.Should().Be(2);
      allocator.GetAllocatedMemorySize(memory1).Should().Be(16);

      var memory2 = allocator.Malloc(256);
      memory2.Segment.Should().Be(SEGMENT);
      memory2.Offset.Should().Be(18);
      allocator.GetAllocatedMemorySize(memory2).Should().Be(256);

      var memory3 = allocator.Malloc(16);
      memory3.Segment.Should().Be(SEGMENT);
      memory3.Offset.Should().Be(256 + 18);
      allocator.GetAllocatedMemorySize(memory3).Should().Be(16);

      allocator.FreeBlocks.Should().Be(1);

      // free middle one, nothing should be able to be compacted
      allocator.Free(memory2);

      allocator.FreeBlocks.Should().Be(2);

      // free first one, should compact
      allocator.Free(memory1);
      allocator.FreeBlocks.Should().Be(2);

      // free final one, should compact
      allocator.Free(memory3);
      allocator.FreeBlocks.Should().Be(1);

      allocator.RemainingBytes.Should().Be(0xFFFE);
    }

    [Fact]
    public void AllocationAndFreeOutOfOrderRepeatSecondOrder()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);

      var memory1 = allocator.Malloc(16);
      memory1.Segment.Should().Be(SEGMENT);
      memory1.Offset.Should().Be(2);

      var memory2 = allocator.Malloc(256);
      memory2.Segment.Should().Be(SEGMENT);
      memory2.Offset.Should().Be(18);

      var memory3 = allocator.Malloc(16);
      memory3.Segment.Should().Be(SEGMENT);
      memory3.Offset.Should().Be(256 + 18);

      allocator.FreeBlocks.Should().Be(1);

      // free middle one, nothing should be able to be compacted
      allocator.Free(memory2);

      allocator.FreeBlocks.Should().Be(2);

      // free last one, should compact
      allocator.Free(memory3);
      allocator.FreeBlocks.Should().Be(1);

      // free first one, should compact
      allocator.Free(memory1);
      allocator.FreeBlocks.Should().Be(1);

      allocator.RemainingBytes.Should().Be(0xFFFE);
    }

    private static void FreeRandom(Random random, List<FarPtr> allocatedMemory, MemoryAllocator allocator)
    {
      var index = random.Next(allocatedMemory.Count);

      allocator.Free(allocatedMemory[index]);

      allocatedMemory.RemoveAt(index);
    }

    [Fact]
    public void AllocateEverything()
    {
      var random = new Random();
      var memory = new List<FarPtr>();
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE, DEFAULT_ALIGNMENT);
      // allocate all the memory
      while (allocator.RemainingBytes > 0)
      {
        var size = random.Next(Math.Min(256, (int)allocator.RemainingBytes)) + 1;
        var ptr = allocator.Malloc((ushort)size);
        // could be null due to fragmentation of the memory space, so just skip and move on to another size
        if (ptr.IsNull())
          continue;

        memory.Add(ptr);

        // randomly free in the middle of the allocations to fragment memory space on purpose
        if (random.Next(4) == 0)
        {
          FreeRandom(random, memory, allocator);
        }
      }

      // ensure future requests fail since we're out of memory
      allocator.Malloc(0).Should().Be(FarPtr.Empty);
      allocator.Malloc(1).Should().Be(FarPtr.Empty);

      while (memory.Count > 0)
      {
         FreeRandom(random, memory, allocator);
      }

      allocator.RemainingBytes.Should().Be(0xFFFE);
      allocator.FreeBlocks.Should().Be(1);
    }

    [Fact]
    public void AlignmentAllocations()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x1000, alignment: 16);
      int expectedAllocations = 0x1000 / 32;
      int allocations = 0;
      for (FarPtr ptr = allocator.Malloc(31); !ptr.IsNull(); ptr = allocator.Malloc(31))
      {
        allocator.GetAllocatedMemorySize(ptr).Should().Be(32);
        ptr.IsAligned(16).Should().BeTrue();
        ++allocations;
      }

      allocations.Should().Be(expectedAllocations);
    }
  }
}
