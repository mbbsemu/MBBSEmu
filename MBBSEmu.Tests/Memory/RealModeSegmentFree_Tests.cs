using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class RealModeSegmentFree_Tests
    {
        private readonly QueueTarget _messages = new();
        private readonly ProtectedModeMemoryCore _memory;

        public RealModeSegmentFree_Tests()
        {
            _memory = new ProtectedModeMemoryCore(new MessageLogger(_messages));
        }

        [Fact]
        public void Free_ReleasesOnlyTheAllocatedRealModeSegment()
        {
            var first = _memory.AllocateRealModeSegment();
            var second = _memory.AllocateRealModeSegment();
            var heap = _memory.Malloc(32);

            _memory.Free(first);

            Assert.False(_memory.HasSegment(first.Segment));
            Assert.True(_memory.HasSegment(second.Segment));
            Assert.True(_memory.HasSegment(heap.Segment));
            Assert.Empty(_messages.DequeueAll());
        }

        [Fact]
        public void Free_RejectsAnInteriorRealModePointer()
        {
            var pointer = _memory.AllocateRealModeSegment();

            _memory.Free(new FarPtr(pointer.Segment, 2));

            Assert.True(_memory.HasSegment(pointer.Segment));
            Assert.Single(_messages.DequeueAll());
            _memory.Free(pointer);
            Assert.False(_memory.HasSegment(pointer.Segment));
        }

        [Fact]
        public void Free_RejectsADoubleFree()
        {
            var pointer = _memory.AllocateRealModeSegment();
            _memory.Free(pointer);
            Assert.Empty(_messages.DequeueAll());

            _memory.Free(pointer);

            Assert.Single(_messages.DequeueAll());
        }

        [Fact]
        public void Free_DoesNotAcceptAnArbitraryExistingSegment()
        {
            _memory.AddSegment(5);

            _memory.Free(new FarPtr(5, 0));

            Assert.True(_memory.HasSegment(5));
            Assert.Single(_messages.DequeueAll());
        }

        [Fact]
        public void Free_DoesNotReleaseAnEntireHeapSegment()
        {
            var pointer = _memory.Malloc(32);

            _memory.Free(new FarPtr(pointer.Segment, 0));

            Assert.True(_memory.HasSegment(pointer.Segment));
            Assert.Equal(32, _memory.GetAllocatedMemorySize(pointer));
            Assert.Single(_messages.DequeueAll());
        }

        [Fact]
        public void Free_PreservesOrdinaryHeapReuse()
        {
            var pointer = _memory.Malloc(32);

            _memory.Free(pointer);

            Assert.Equal(pointer, _memory.Malloc(32));
            Assert.Empty(_messages.DequeueAll());
        }

        [Fact]
        public void Free_DoesNotReuseAReleasedSelector()
        {
            var first = _memory.AllocateRealModeSegment();
            _memory.Free(first);
            var second = _memory.AllocateRealModeSegment();

            Assert.NotEqual(first.Segment, second.Segment);
            _memory.Free(first);
            Assert.True(_memory.HasSegment(second.Segment));
        }

        [Fact]
        public void Clear_ForgetsRealModeSegmentOwnership()
        {
            var pointer = _memory.AllocateRealModeSegment();
            _memory.Clear();
            _memory.AddSegment(pointer.Segment);

            _memory.Free(pointer);

            Assert.True(_memory.HasSegment(pointer.Segment));
            Assert.Single(_messages.DequeueAll());
        }

        [Fact]
        public void RemoveSegment_ForgetsRealModeSegmentOwnership()
        {
            var pointer = _memory.AllocateRealModeSegment();
            _memory.RemoveSegment(pointer.Segment);
            _memory.AddSegment(pointer.Segment);

            _memory.Free(pointer);

            Assert.True(_memory.HasSegment(pointer.Segment));
            Assert.Single(_messages.DequeueAll());
        }

        [Fact]
        public void Free_NullIsStillHarmless()
        {
            _memory.Free(FarPtr.Empty);

            Assert.Empty(_messages.DequeueAll());
        }
    }
}
