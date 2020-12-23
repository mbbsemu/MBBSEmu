using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using System.Collections.Generic;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class anpbtv_Tests : ExportedModuleTestBase
    {
        private const ushort RECORD_LENGTH = 80;
        private const int ABSBTV = 53;
        private const int QRYBTV = 485;
        private const int ANPBTV = 70;

        [Fact]
        public void anpbtv_noBB()
        {
          Reset();

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { 0, 0, 0});

          mbbsEmuCpuRegisters.AX.Should().Be(0);
        }

        [Fact]
        public void anpbtv()
        {
          Reset();

          AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

          var keyPtr = mbbsEmuMemoryCore.AllocateVariable(null, 128);
          mbbsEmuMemoryCore.SetArray(keyPtr, Encoding.ASCII.GetBytes("test"));

          // have to query first, seek to "test"
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, QRYBTV, new List<ushort> { keyPtr.Offset, keyPtr.Segment, /* key= */ 0, (ushort)EnumBtrieveOperationCodes.QueryEqual });
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);

          // verify absolute position
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(2);

          // do the actual test now, which seeks next to "yyz"
          var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.AcquireNext});
          mbbsEmuCpuRegisters.AX.Should().NotBe(0);
          Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(record, 4)).Should().BeEquivalentTo("yyz\0");

          // verify absolute position
          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ABSBTV, new List<ushort> {});
          mbbsEmuCpuRegisters.GetLong().Should().Be(3);
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
                    Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 0,
                    Length = 32,
                    Segment = false,
                });

            btrieveFile.Keys.Add(0, key);

            btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("abc")));
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("test")));
            btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("yyz")));

            return btrieveFile;
        }

        private static byte[] CreateRecord(string value)
        {
          var ret = new byte[RECORD_LENGTH];
          Array.Copy(Encoding.ASCII.GetBytes(value), ret, value.Length);
          return ret;
        }
    }
}
