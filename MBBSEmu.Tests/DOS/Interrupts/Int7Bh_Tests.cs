using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Resources;
using MBBSEmu.Testing;
using MBBSEmu.Tests;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace MBBSEmu.DOS.Interrupts
{
    /* Data layout as follows:

      sqlite> select * from data_t;
          id          data        key_0       key_1       key_2       key_3
          ----------  ----------  ----------  ----------  ----------  ----------
          1                       Sysop       3444        3444        1
          2                       Sysop       7776        7776        2
          3                       Sysop       1052234073  StringValu  3
          4                       Sysop       -615634567  stringValu  4
      */

    public class Int7Bh_Tests : TestBase, IDisposable
  {
    private readonly string[] _runtimeFiles = { "MBBSEMU.DB" };

    private readonly ICpuRegisters _registers = new CpuRegisters();
    private readonly FakeClock _fakeClock = new FakeClock();
    private readonly ServiceResolver _serviceResolver;
    private readonly IMemoryCore _memory;
    private readonly Int7Bh _int7B;
    private readonly string _modulePath;
    private readonly FarPtr _dataBuffer;
    private readonly FarPtr _statusCodePointer;

    public Int7Bh_Tests()
    {
        _modulePath = GetModulePath();

        _serviceResolver = new ServiceResolver(SessionBuilder.ForTest($"MBBSExeRuntime_{RANDOM.Next()}"));

        Directory.CreateDirectory(_modulePath);

        CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

        _serviceResolver = new ServiceResolver(_fakeClock);

        _memory = RealModeMemoryCore.GetInstance(_serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>());
        _int7B = new Int7Bh(_serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(), _modulePath, _serviceResolver.GetService<IFileUtility>(), _registers, _memory);

        _dataBuffer = _memory.Malloc(Int7Bh.BTRIEVE_COMMAND_STRUCT_LENGTH);
        _statusCodePointer = _memory.Malloc(2);
    }

    public void Dispose()
    {
        _memory.Free(_dataBuffer);
        _memory.Free(_statusCodePointer);

        _int7B.Dispose();

        Directory.Delete(_modulePath, recursive: true);
    }

    private void CopyModuleToTempPath(IResourceManager resourceManager)
    {
        foreach (var file in _runtimeFiles)
        {
            File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
        }
    }

    [Fact]
    public void InvalidInterface()
    {
      var command = new DOSInterruptBtrieveCommand()
      {
        interface_id = 0xDEAD,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      // pull out the status and verify
      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidInterface);
    }

    [Fact]
    public void StatWithoutOpeningDatabase()
    {
      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Stat,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      // pull out the status and verify
      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.FileNotOpen);
    }

    private FarPtr OpenDatabase()
    {
      var positionBlock = _memory.Malloc(64);
      var fileName = _memory.Malloc(16);
      _memory.SetArray(fileName, Encoding.ASCII.GetBytes("MBBSEMU.DAT\0"));

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Open,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        key_buffer_segment = fileName.Segment,
        key_buffer_offset = fileName.Offset,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // position block should have some data written to it
      _memory.GetArray(positionBlock, 64).ToArray().All(o => o == 0).Should().BeFalse();

      return positionBlock;
    }

    [Fact]
    public void OpenStatLengthOverrunAndSpanSupport()
    {
      var positionBlock = OpenDatabase();

      var dataBuffer = _memory.Malloc(1024);
      var spanString = _memory.Malloc(1);
      _memory.SetByte(spanString, 0xFF);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Stat,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        key_buffer_segment = spanString.Segment,
        key_buffer_offset = spanString.Offset,
        key_buffer_length = 1, // we don't support spanning databases, so should return empty string back
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 0, // this causes the overrun
      };

      Handle(command);

      _memory.GetByte(spanString).Should().Be(0); // no span support (i.e. empty string)
      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DataBufferLengthOverrun);
    }

    [Fact]
    public void Stat()
    {
      var positionBlock = OpenDatabase();

      var dataBuffer = _memory.Malloc(1024);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Stat,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 1024,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data is a BtrieveFileSpec followed by BtrieveKeySpec for each key
      var ptr = dataBuffer;
      var size = Marshal.SizeOf(typeof(Int7Bh.BtrieveFileSpec));
      var btrieveFileSpec = Int7Bh.ByteArrayToStructure<Int7Bh.BtrieveFileSpec>(_memory.GetArray(ptr, (ushort) size).ToArray());

      btrieveFileSpec.record_length.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
      btrieveFileSpec.number_of_keys.Should().Be(4);
      btrieveFileSpec.number_of_records.Should().Be(4);
      btrieveFileSpec.flags.Should().Be(0);

      ptr += size;
      size = Marshal.SizeOf(typeof(Int7Bh.BtrieveKeySpec));
      Int7Bh.ByteArrayToStructure<Int7Bh.BtrieveKeySpec>(_memory.GetArray(ptr + 0 * size, (ushort) size).ToArray()).Should().BeEquivalentTo(
          new Int7Bh.BtrieveKeySpec()
          {
              flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType),
              data_type = (byte) EnumKeyDataType.Zstring,
              position = 3,
              length = 32,
          });

      Int7Bh.ByteArrayToStructure<Int7Bh.BtrieveKeySpec>(_memory.GetArray(ptr + 1 * size, (ushort) size).ToArray()).Should().BeEquivalentTo(
          new Int7Bh.BtrieveKeySpec()
          {
              flags = (ushort) (EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),
              data_type = (byte) EnumKeyDataType.Integer,
              position = 35,
              length = 4,
          });
      Int7Bh.ByteArrayToStructure<Int7Bh.BtrieveKeySpec>(_memory.GetArray(ptr + 2 * size, (ushort) size).ToArray()).Should().BeEquivalentTo(
          new Int7Bh.BtrieveKeySpec()
          {
              flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),
              data_type = (byte) EnumKeyDataType.Zstring,
              position = 39,
              length = 32,
          });
      Int7Bh.ByteArrayToStructure<Int7Bh.BtrieveKeySpec>(_memory.GetArray(ptr + 3 * size, (ushort) size).ToArray()).Should().BeEquivalentTo(
          new Int7Bh.BtrieveKeySpec()
          {
              flags = (ushort) EnumKeyAttributeMask.UseExtendedDataType,
              data_type = (byte) EnumKeyDataType.AutoInc,
              position = 71,
              length = 4,
          });

          // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be((ushort) (Marshal.SizeOf(typeof(Int7Bh.BtrieveFileSpec)) + 4 * Marshal.SizeOf(typeof(Int7Bh.BtrieveKeySpec))));
    }

    [Fact]
    public void OpenAndClose()
    {
      var positionBlock = OpenDatabase();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Close,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
    }

    [Fact]
    public void StepBufferOverrun()
    {
      var positionBlock = OpenDatabase();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.StepLast,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_length = 0,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DataBufferLengthOverrun);
    }

    [Fact]
    public void StepLastAndGetPosition()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(1024);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.StepLast,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 1024,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
      // data should contain the proper record values
      new MBBSEmuRecordStruct(_memory.GetArray(dataBuffer, MBBSEmuRecordStruct.RECORD_LENGTH).ToArray()).Key1.Should().Be(-615634567);

      // GetPosition
      command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetPosition,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 4,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(4);
      // position should be 4 now since we have 4 records
      _memory.GetDWord(dataBuffer).Should().Be(4);
    }

    [Fact]
    public void QueryKeyBufferTooShort()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.AcquireEqual,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 16, // should be 32
        key_number = 2
      };

      _memory.SetArray(keyBuffer, Encoding.ASCII.GetBytes("StringValue\0"));

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.KeyBufferTooShort);
    }

    [Fact]
    public void QueryDataBufferLengthOverrun()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.AcquireEqual,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 0,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 2
      };

      _memory.SetArray(keyBuffer, Encoding.ASCII.GetBytes("StringValue\0"));

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DataBufferLengthOverrun);
    }

    [Fact]
    public void Query()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.AcquireEqual,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 2
      };

      _memory.SetArray(keyBuffer, Encoding.ASCII.GetBytes("StringValue\0"));

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
      // data should contain the proper record values
      new MBBSEmuRecordStruct(_memory.GetArray(dataBuffer, MBBSEmuRecordStruct.RECORD_LENGTH).ToArray()).Key2.Should().Be("StringValue");
      Encoding.ASCII.GetString(_memory.GetString(keyBuffer, true)).Should().Be("StringValue");
    }

    [Fact]
    public void Insert()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Insert,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 0
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
      // key should have been returned
      Encoding.ASCII.GetString(_memory.GetString(keyBuffer, true)).Should().Be("Paladine");
      // did we add the record?
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(5);
    }

    [Fact]
    public void InsertDuplicateKeyValue()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Insert,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 0
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Sysop", Key1 = 7776, Key2 = "In orbe terrarum, optimus sum" };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DuplicateKeyValue);
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);
    }

    [Fact]
    public void InsertKeyBufferTooShort()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Insert,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 0,
        key_number = 0
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.KeyBufferTooShort);

      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);
    }

    [Fact]
    public void Delete()
    {
      // StepLast
      var positionBlock = OpenDatabase();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Delete,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);

      // did we delete the record?
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(3);
    }

    [Fact]
    public void DeleteEmpty()
    {
      // StepLast
      var positionBlock = OpenDatabase();

      GetBtrieveFileProcessor(positionBlock).DeleteAll();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Delete,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidPositioning);

      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(0);
    }

    [Fact]
    public void UpdateNonModifiableFailure()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Update,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 0
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.NonModifiableKeyValue);

      // assert value didn't change
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);

      record = new MBBSEmuRecordStruct(GetBtrieveFileProcessor(positionBlock).GetRecord());
      record.Key0.Should().Be("Sysop");
      record.Key1.Should().Be(3444);
      record.Key2.Should().Be("3444");
    }

    [Fact]
    public void UpdateKeyConstraintFailure()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Update,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 1
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Sysop", Key1 = 7776, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DuplicateKeyValue);
      // assert value didn't change
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);

      record = new MBBSEmuRecordStruct(GetBtrieveFileProcessor(positionBlock).GetRecord());
      record.Key0.Should().Be("Sysop");
      record.Key1.Should().Be(3444);
      record.Key2.Should().Be("3444");
    }

    [Fact]
    public void UpdateKeyBufferTooShort()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Update,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 1,
        key_number = 1
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Sysop", Key1 = 7776, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.KeyBufferTooShort);
      // assert value didn't change
      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);

      record = new MBBSEmuRecordStruct(GetBtrieveFileProcessor(positionBlock).GetRecord());
      record.Key0.Should().Be("Sysop");
      record.Key1.Should().Be(3444);
      record.Key2.Should().Be("3444");
    }

    [Fact]
    public void Update()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(MBBSEmuRecordStruct.RECORD_LENGTH);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.Update,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = MBBSEmuRecordStruct.RECORD_LENGTH,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 1
      };

      var record = new MBBSEmuRecordStruct { Key0 = "Sysop", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };
      _memory.SetArray(dataBuffer, record.Data);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);

      // key value should be returned
      _memory.GetDWord(keyBuffer).Should().Be(31337);

      GetBtrieveFileProcessor(positionBlock).GetRecordCount().Should().Be(4);

      record = new MBBSEmuRecordStruct(GetBtrieveFileProcessor(positionBlock).GetRecord());
      record.Key0.Should().Be("Sysop");
      record.Key1.Should().Be(31337);
      record.Key2.Should().Be("In orbe terrarum, optimus sum");
    }

    [Fact]
    public void GetChunkUnsupported()
    {
      // StepLast
      var positionBlock = OpenDatabase();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        key_number = -2
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidOperation);
    }

    [Fact]
    public void GetDirectRecordKeyBufferTooShort()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 1,
        key_number = 1
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.KeyBufferTooShort);
    }

    [Fact]
    public void GetDirectRecordBadIndex()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(1024);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 1024,
        key_number = -1, // no logical currency
      };

      // data buffer contains the physical offset to read
      _memory.SetDWord(dataBuffer, 0);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidPositioning);

      // data buffer contains the physical offset to read
      _memory.SetDWord(dataBuffer, 5);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidPositioning);
    }

    [Fact]
    public void GetDirectRecordDataBufferLengthOverrun()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(1024);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 1,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 1, // no logical currency
      };

      // data buffer contains the physical offset to read
      _memory.SetDWord(dataBuffer, 2);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.DataBufferLengthOverrun);

      // now we need to validate logical currency by stepping through based on key_number == 1, there is one previous record, and then nothing
      var btrieve = GetBtrieveFileProcessor(positionBlock);
      btrieve.PerformOperation(1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
      btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
    }

    [Fact]
    public void GetDirectRecord()
    {
      // StepLast
      var positionBlock = OpenDatabase();
      var dataBuffer = _memory.Malloc(1024);
      var keyBuffer = _memory.Malloc(32);

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
        data_buffer_segment = dataBuffer.Segment,
        data_buffer_offset = dataBuffer.Offset,
        data_buffer_length = 1024,
        key_buffer_segment = keyBuffer.Segment,
        key_buffer_offset = keyBuffer.Offset,
        key_buffer_length = 32,
        key_number = 1,
      };

      // data buffer contains the physical offset to read
      _memory.SetDWord(dataBuffer, 2);

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.Success);
      // data_buffer_length should be updated
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
      // data should contain the proper record values
      new MBBSEmuRecordStruct(_memory.GetArray(dataBuffer, MBBSEmuRecordStruct.RECORD_LENGTH).ToArray()).Key1.Should().Be(7776);
      _memory.GetDWord(keyBuffer).Should().Be(7776);

      // now we need to validate logical currency by stepping through based on key_number == 1, there is one previous record, and then nothing
      var btrieve = GetBtrieveFileProcessor(positionBlock);
      btrieve.PerformOperation(1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
      btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);

      btrieve.PerformOperation(1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
      btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

      btrieve.PerformOperation(1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();
    }

    [Fact]
    public void InvalidOperation()
    {
      // StepLast
      var positionBlock = OpenDatabase();

      var command = new DOSInterruptBtrieveCommand()
      {
        operation = EnumBtrieveOperationCodes.SetOwner,
        interface_id = Int7Bh.EXPECTED_INTERFACE_ID,
        position_block_segment = positionBlock.Segment,
        position_block_offset = positionBlock.Offset,
        status_code_pointer_segment = _statusCodePointer.Segment,
        status_code_pointer_offset = _statusCodePointer.Offset,
      };

      Handle(command);

      _memory.GetWord(_statusCodePointer).Should().Be((ushort) BtrieveError.InvalidOperation);
    }

    // TODO - read direct logical currency

    private void Handle(DOSInterruptBtrieveCommand command)
    {
      _memory.SetArray(_dataBuffer, StructureToByteArray(command).AsSpan());
      _registers.DS = _dataBuffer.Segment;
      _registers.DX = _dataBuffer.Offset;

      _int7B.Handle();
    }

    private byte[] StructureToByteArray(object obj)
    {
      var len = Marshal.SizeOf(obj);
      var ret = new byte[len];
      var ptr = Marshal.AllocHGlobal(len);
      Marshal.StructureToPtr(obj, ptr, true);
      Marshal.Copy(ptr, ret, 0, len);
      Marshal.FreeHGlobal(ptr);
      return ret;
    }

    private BtrieveFileProcessor GetBtrieveFileProcessor(FarPtr positionBlock) => _int7B.GetFromGUID(new Guid(_memory.GetArray(positionBlock, 16).ToArray()));
  }
}
