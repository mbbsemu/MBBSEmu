using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class anpbtv_Tests : ExportedModuleTestBase
    {
        const ushort RECORD_LENGTH = 80;
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

          ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ANPBTV, new List<ushort> { 0, 0, 0});

          mbbsEmuCpuRegisters.AX.Should().Be(0);
        }

        private BtrieveFile CreateBtrieveFile()
        {
          var btrieveFile = new BtrieveFile()
            {
                RecordLength = RECORD_LENGTH,
                FileName = $"{RANDOM.Next() % 100_000_000}.DAT",
                RecordCount = 0,
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

            //btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("Sysop")));
            //btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("Paladine")));
            //btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("Testing")));

            return btrieveFile;
        }
    }
}
