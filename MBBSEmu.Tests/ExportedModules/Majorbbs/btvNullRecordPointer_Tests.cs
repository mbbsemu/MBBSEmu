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
    /// <summary>
    ///     A NULL record pointer passed to a Btrieve write means the current file's own record
    ///     buffer, bb->data, as in MajorBBS (PLBTVSTF.C)
    /// </summary>
    public class btvNullRecordPointer_Tests : ExportedModuleTestBase
    {
        private const ushort RECORD_LENGTH = 80;
        private const int STPBTV = 569;
        private const int UPDBTV = 621;
        private const int DUPDBTV = 180;
        private const int UPVBTV = 622;
        private const int INSBTV = 351;
        private const int DINSBTV = 170;
        private const int INVBTV = 357;

        [Theory]
        [InlineData(UPDBTV, false)]
        [InlineData(DUPDBTV, false)]
        [InlineData(UPVBTV, true)]
        public void update_null_record_pointer_writes_bb_data(int ordinal, bool takesLength)
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            //Read the first record into bb->data
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPBTV, new List<ushort> { 0, 0, (ushort)EnumBtrieveOperationCodes.StepFirst });
            mbbsEmuCpuRegisters.AX.Should().Be(1);
            mbbsEmuMemoryCore.GetByte(GetBB().data + (RECORD_LENGTH - 1)).Should().Be(1);

            //Change it there and update with a NULL record pointer
            mbbsEmuMemoryCore.SetByte(GetBB().data + (RECORD_LENGTH - 1), 0x77);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal,
                takesLength ? new List<ushort> { 0, 0, RECORD_LENGTH } : new List<ushort> { 0, 0 });
            if (ordinal == DUPDBTV)
                mbbsEmuCpuRegisters.AX.Should().Be(1);

            //Read it back into a separate buffer
            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.StepFirst });
            mbbsEmuCpuRegisters.AX.Should().Be(1);
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(record, stripNull: true)).Should().Be("ABC");
            mbbsEmuMemoryCore.GetByte(record + (RECORD_LENGTH - 1)).Should().Be(0x77);
        }

        [Theory]
        [InlineData(INSBTV, false)]
        [InlineData(DINSBTV, false)]
        [InlineData(INVBTV, true)]
        public void insert_null_record_pointer_writes_bb_data(int ordinal, bool takesLength)
        {
            Reset();

            AllocateBB(CreateBtrieveFile(), RECORD_LENGTH);

            mbbsEmuMemoryCore.SetArray(GetBB().data, CreateRecord("new", 9));
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal,
                takesLength ? new List<ushort> { 0, 0, RECORD_LENGTH } : new List<ushort> { 0, 0 });
            if (ordinal == DINSBTV)
                mbbsEmuCpuRegisters.AX.Should().Be(1);

            var record = mbbsEmuMemoryCore.AllocateVariable(null, RECORD_LENGTH);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPBTV, new List<ushort> { record.Offset, record.Segment, (ushort)EnumBtrieveOperationCodes.StepLast });
            mbbsEmuCpuRegisters.AX.Should().Be(1);
            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(record, stripNull: true)).Should().Be("new");
            mbbsEmuMemoryCore.GetByte(record + (RECORD_LENGTH - 1)).Should().Be(9);
        }

        private BtvFileStruct GetBB() =>
            new(mbbsEmuMemoryCore.GetArray(mbbsEmuMemoryCore.GetPointer("BB"), BtvFileStruct.Size));

        private BtrieveFile CreateBtrieveFile()
        {
            var btrieveFile = new BtrieveFile()
            {
                RecordLength = RECORD_LENGTH,
                FileName = $"{RANDOM.Next() % 100_000_000}.DAT",
                RecordCount = 2,
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
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("test", 2)));

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
