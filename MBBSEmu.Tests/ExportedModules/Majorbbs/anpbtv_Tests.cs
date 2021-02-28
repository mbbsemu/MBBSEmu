using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const ushort RECORD_LENGTH = 80;
        private const int ABSBTV = 53;
        private const int QRYBTV = 485;
        private const int ANPBTV = 70;
        private const int ANPBTVL = 913;

        [Fact]
        public void anpbtv_noBB()
        {
          Reset();

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { 0, 0, 0});

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
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(5);

          // do the actual test now, which seeks to "yyz", but fails since the key is different
          var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().Be(0);

          // verify absolute position - side effect is that it does complete the move next
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
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
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(1);

          // do the actual test now, which seeks to the next "ABC"
          var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);
          mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(2);

          // verify absolute position
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(2);

          // Try to seek again, should fail since case sensitive
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().Be(0);

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
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
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(1);

          // do the actual test now, which seeks to the next "ABC"
          var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);
          mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(2);

          // verify absolute position
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(2);

          // Try to seek again, should succeed to "abc" since case insensitive
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);
          mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(3);

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(3);

          // Last seek, should succeed to final "abc" since case insensitive
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);
          mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(4);

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(4);

          // Last will fail
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTVL, new List<ushort> { record.Offset, record.Segment, /* chkcas= */ 0, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().Be(0);
          mbbsEmuMemoryCore.GetByte(record + RECORD_LENGTH - 1).Should().Be(5);

          // verify absolute position
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(5);
        }
        private BtrieveFile CreateBtrieveFile()
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
                    Offset = 0,
                    Length = 32,
                    Segment = false,
                });

            btrieveFile.Keys.Add(0, key);

            btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("ABC", 1)));
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("ABC", 2)));
            btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("abc", 3)));
            btrieveFile.Records.Add(new BtrieveRecord(4, CreateRecord("abc", 4)));
            btrieveFile.Records.Add(new BtrieveRecord(5, CreateRecord("test", 5)));
            btrieveFile.Records.Add(new BtrieveRecord(6, CreateRecord("yyz", 6)));

            return btrieveFile;
        }

        private static byte[] CreateRecord(string value, byte lastByte)
        {
          var ret = new byte[RECORD_LENGTH];
          Array.Copy(Encoding.ASCII.GetBytes(value), ret, value.Length);
          ret[RECORD_LENGTH - 1] = lastByte;
          return ret;
        }
    }
}
