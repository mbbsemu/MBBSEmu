using FluentAssertions;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class RealModeMemoryCore_Tests : TestBase
    {
        private readonly IMessageLogger _logger = new ServiceResolver().GetService<LogFactory>().GetLogger<MessageLogger>();

        /// <summary>
        ///     Allocates three pointers, verifies they all exist on segments (offsets 0), frees
        ///     them all and then verifies the next allocations returns the original allocation
        ///     address.
        /// </summary>
        [Fact]
        public void AllocationAndFree()
        {
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);

            var ptr1 = memoryCore.Malloc(256);
            ptr1.Offset.Should().Be(0);

            var ptr2 = memoryCore.Malloc(512);
            ptr2.Offset.Should().Be(0);
            (ptr2.Segment - ptr1.Segment).Should().Be(256 / 16);

            var ptr3 = memoryCore.Malloc(512);
            ptr3.Offset.Should().Be(0);
            (ptr3.Segment - ptr2.Segment).Should().Be(512 / 16);

            memoryCore.Free(ptr2);
            memoryCore.Free(ptr1);
            memoryCore.Free(ptr3);

            var ptr = memoryCore.Malloc(0);
            ptr.Should().Be(ptr1);
        }

        [Fact]
        public void VariableAllocation()
        {
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);

            memoryCore.TryGetVariablePointer("TEST", out var ptr).Should().BeFalse();
            ptr = memoryCore.AllocateVariable("TEST", 16);
            ptr.IsNull().Should().BeFalse();
            ptr.Offset.Should().Be(0);

            memoryCore.TryGetVariablePointer("TEST", out var ptr2).Should().BeTrue();
            ptr2.Should().Be(ptr);

            var ptr3 = memoryCore.GetOrAllocateVariablePointer("TEST", 16);
            ptr3.Should().Be(ptr);

            var ptr4 = memoryCore.GetOrAllocateVariablePointer("ANOTHER", 16);
            ptr4.Should().NotBe(ptr);
            ptr4.Offset.Should().Be(0);
        }

        [Fact]
        public void MemoryMap_Byte()
        {
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetByte(ptr1, 0x10);
            (memoryCore as IMemoryCore).GetByte(ptr1).Should().Be(0x10);
            (memoryCore as IMemoryCore).GetByte(ptr2).Should().Be(0x10);
        }

        [Fact]
        public void MemoryMap_Word()
        {
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetWord(ptr1, 0x1020);
            (memoryCore as IMemoryCore).GetWord(ptr1).Should().Be(0x1020);
            (memoryCore as IMemoryCore).GetWord(ptr2).Should().Be(0x1020);
        }

        [Fact]
        public void MemoryMap_DWord()
        {
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetDWord(ptr1, 0x10203040);
            (memoryCore as IMemoryCore).GetDWord(ptr1).Should().Be(0x10203040);
            (memoryCore as IMemoryCore).GetDWord(ptr2).Should().Be(0x10203040);
        }

        [Fact]
        public void MemoryMap_Array()
        {
            var bytes = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 };
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetArray(ptr1, bytes);
            (memoryCore as IMemoryCore).GetArray(ptr1, (ushort)bytes.Length).ToArray().Should().BeEquivalentTo(bytes);
            (memoryCore as IMemoryCore).GetArray(ptr2, (ushort)bytes.Length).ToArray().Should().BeEquivalentTo(bytes);
        }

        [Fact]
        public void MemoryMap_StringNoStripNull()
        {
            var str = "This is a test\0";
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetArray(ptr1, Encoding.ASCII.GetBytes(str));
            Encoding.ASCII.GetString((memoryCore as IMemoryCore).GetString(ptr1, stripNull: false)).Should().Be(str);
            Encoding.ASCII.GetString((memoryCore as IMemoryCore).GetString(ptr2, stripNull: false)).Should().Be(str);
        }

        [Fact]
        public void MemoryMap_StringStripNull()
        {
            var str = "This is a test";
            var memoryCore = RealModeMemoryCore.GetInstance(_logger);
            // these two point to the same physical address
            var ptr1 = new FarPtr(0x6002, 0x12);
            var ptr2 = new FarPtr(0x6003, 2);

            (memoryCore as IMemoryCore).SetArray(ptr1, Encoding.ASCII.GetBytes(str + "\0"));
            Encoding.ASCII.GetString((memoryCore as IMemoryCore).GetString(ptr1, stripNull: true)).Should().Be(str);
            Encoding.ASCII.GetString((memoryCore as IMemoryCore).GetString(ptr2, stripNull: true)).Should().Be(str);
        }
    }
}
