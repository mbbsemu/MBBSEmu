using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.HostProcess.Structs;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class anpbtv_Tests : ExportedModuleTestBase
    {
        private const ushort RECORD_LENGTH = 80;
        private const int ABSBTV = 53;
        private const int QRYBTV = 485;
        private const int QNPBTV = 484;
        private const int GABBTV = 999;
        private const int ANPBTV = 70;
        private const int ANPBTVL = 913;
        private const int STPBTV = 569;

        [Fact]
        public void anpbtv_noBB()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { 0, 0, 0 });

            mbbsEmuCpuRegisters.AX.Should().Be(0);
        }

        [Fact]
        public void anpbtv_no_duplicates()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("test"));

            // have to query first, seek to "test"
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(5);

            // do the actual test now, which seeks to "yyz", but fails since the key is different
            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().Be(0);

            // verify absolute position - side effect is that it does complete the move next
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(6);
        }

        [Fact]
        public void anpbtv_duplicates_case_sensitive()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("ABC"));

            // have to query first, seek to "ABC"
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(1);

            // do the actual test now, which seeks to the next "ABC"
            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(2);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(2);

            // Try to seek again, should fail since case sensitive
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().Be(0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(3);
        }

        [Fact]
        public void anpbtv_duplicates_case_insensitive()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("ABC"));

            // have to query first, seek to "ABC"
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(1);

            // do the actual test now, which seeks to the next "ABC"
            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(2);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(2);

            // Try to seek again, should succeed to "abc" since case insensitive
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(3);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(3);

            // Last seek, should succeed to final "abc" since case insensitive
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(4);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(4);

            // Last will fail
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().Be(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(5);

            // verify absolute position
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(5);
        }

        [Fact]
        public void qnpbtv_after_gabbtv_continues_from_absolute_position()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("ABC"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(1);

            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GABBTV, new List<ushort> { record.Offset, record.Segment, /* abspos= */ 5, 0, /* key= */ 0 });
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(5);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QNPBTV, new List<ushort> { (ushort)EnumBtrieveOperationCodes.QueryNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> { });
            mbbsEmuCpuRegisters.GetLong().Should().Be(6);
        }

        [Fact]
        public void anpbtv_null_recptr_compares_against_record()
        {
            Reset();

            // the key starts at offset 4, after a "XYZ" prefix
            AllocateBB(CreateBtrieveFile(keyOffset: 4), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("ABC"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            // the next record has the same key, but with no recptr it's read into BB's data
            // buffer over the saved key, so the comparison is "XYZ" against "ABC" and fails
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { 0, 0, (ushort)EnumBtrieveOperationCodes.AcquireNext });
            mbbsEmuCpuRegisters.AX.Should().Be(0);

            var btvStruct = GetBB();
            mbbsEmuMemoryCore.GetByte(btvStruct.data + RECORD_LENGTH - 1).Should().Be(2);
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(btvStruct.key, stripNull: true)).Should().Be("ABC");
        }

        [Fact]
        public void qnpbtv_reads_record_into_bb()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("abc"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QNPBTV, new List<ushort> { (ushort)EnumBtrieveOperationCodes.QueryNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            var btvStruct = GetBB();
            mbbsEmuMemoryCore.GetByte(btvStruct.data + RECORD_LENGTH - 1).Should().Be(4);
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(btvStruct.key, stripNull: true)).Should().Be("abc");

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QNPBTV, new List<ushort> { (ushort)EnumBtrieveOperationCodes.QueryNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            mbbsEmuMemoryCore.GetByte(btvStruct.data + RECORD_LENGTH - 1).Should().Be(5);
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(btvStruct.key, stripNull: true)).Should().Be("test");
        }

        [Fact]
        public void stpbtv_leaves_bb_key()
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
            mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("test"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryGreaterOrEqual });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);

            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.StepNext });
            mbbsEmuCpuRegisters.AX.Should().NotBe(0);
            mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(6);

            // Step passes no key buffer, so BB's key still holds the queried key
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(GetBB().key, stripNull: true)).Should().Be("test");
        }

        private BtvFileStruct GetBB() =>
            new(mbbsEmuMemoryCore.GetArray(mbbsEmuMemoryCore.GetPointer("BB"), BtvFileStruct.Size));

        private BtrieveFile CreateBtrieveFile(ushort keyOffset = 0)
        {
            var btrieveFile = new BtrieveFile()
            {
                RecordLength = RECORD_LENGTH,
                FileName = $"{RANDOM.Next() % 100_000_000}.DAT",
                RecordCount = 3,
            };

            var key = new BtrieveKey();
            key.Segments.Add(new BtrieveKeyDefinition()
            {
                Number = 0,
                Attributes = EnumKeyAttributeMask.UseExtendedDataType | EnumKeyAttributeMask.Duplicates,
                DataType = EnumKeyDataType.Zstring,
                Offset = keyOffset,
                Length = 32,
                Segment = false,
            });

            btrieveFile.Keys.Add(0, key);

            btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("ABC", 1, keyOffset)));
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("ABC", 2, keyOffset)));
            btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("abc", 3, keyOffset)));
            btrieveFile.Records.Add(new BtrieveRecord(4, CreateRecord("abc", 4, keyOffset)));
            btrieveFile.Records.Add(new BtrieveRecord(5, CreateRecord("test", 5, keyOffset)));
            btrieveFile.Records.Add(new BtrieveRecord(6, CreateRecord("yyz", 6, keyOffset)));

            return btrieveFile;
        }

        private static byte[] CreateRecord(string value, byte lastByte, ushort keyOffset)
        {
            var ret = new byte[RECORD_LENGTH];
            if (keyOffset > 0)
                Array.Copy(Encoding.ASCII.GetBytes("XYZ"), ret, 3);
            Array.Copy(Encoding.ASCII.GetBytes(value), 0, ret, keyOffset, value.Length);
            ret[RECORD_LENGTH - 1] = lastByte;
            return ret;
        }
    }
}
