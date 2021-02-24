using FluentAssertions;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Memory;
using NLog;
using System;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
  public class MemoryAllocator_Tests
  {
    private const int SEGMENT = 1;

    private readonly ILogger _logger = new ServiceResolver().GetService<ILogger>();

    [Fact]
    public void Properties()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000);
      allocator.BasePointer.Should().Be(new FarPtr(SEGMENT, 0));
      allocator.Size.Should().Be(0x10000);
    }

    [Fact]
    public void ConstructsJustEnough()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0xFFFF), 1);
      allocator.Should().NotBeNull();
    }

    [Fact]
    public void ConstructOverflowsSegment()
    {
      Action act = () => new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0xFFFF), 2);
      act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllocateZero()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000);

      allocator.Malloc(0).Should().Be(FarPtr.Empty);
    }

    [Fact]
    public void FreeNull()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 0), 0x10000);
      allocator.Free(FarPtr.Empty);
    }

    [Fact]
    public void SimpleAllocation()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(1);
    }

    [Fact]
    public void AllocateEntireBlock()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE);

      var memory = allocator.Malloc(0xFFFE);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(0);
    }

    [Fact]
    public void DoubleAllocation()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(18);

      allocator.FreeBlocks.Should().Be(1);
    }

    [Fact]
    public void AllocationAndFreeRepeat()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE);

      var memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.Free(memory);

      memory = allocator.Malloc(16);
      memory.Segment.Should().Be(SEGMENT);
      memory.Offset.Should().Be(2);

      allocator.FreeBlocks.Should().Be(1);
    }

    [Fact]
    public void AllocationAndFreeOutOfOrderRepeat()
    {
      var allocator = new MemoryAllocator(_logger, new FarPtr(SEGMENT, 2), 0xFFFE);

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

      // free first one, should compact
      allocator.Free(memory1);
      allocator.FreeBlocks.Should().Be(2);

      // free final one, should compact
      allocator.Free(memory3);
      allocator.FreeBlocks.Should().Be(1);
    }
  }
}
