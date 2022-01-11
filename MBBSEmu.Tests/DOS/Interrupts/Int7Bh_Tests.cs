using FluentAssertions;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Resources;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Database.Session;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
  public class Int7Bh_Tests : TestBase, IDisposable
  {
    private readonly string[] _runtimeFiles = { "MBBSEMU.DB" };

    private readonly ICpuRegisters _registers = new CpuRegisters();
    private readonly FakeClock _fakeClock = new FakeClock();
    private readonly ServiceResolver _serviceResolver;
    private readonly IMemoryCore _memory;
    private readonly Int7Bh _int7B;
    private string _modulePath;
    private FarPtr _dataBuffer;
    private FarPtr _statusCodePointer;

    public Int7Bh_Tests()
    {
        _modulePath = GetModulePath();

        _serviceResolver = new ServiceResolver(SessionBuilder.ForTest($"MBBSExeRuntime_{RANDOM.Next()}"));

        Directory.CreateDirectory(_modulePath);

        CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

        _serviceResolver = new ServiceResolver(_fakeClock);

        _memory = new RealModeMemoryCore(_serviceResolver.GetService<ILogger>());
        _int7B = new Int7Bh(_serviceResolver.GetService<ILogger>(), _modulePath, _serviceResolver.GetService<IFileUtility>(), _registers, _memory);

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
      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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
      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      btrieveFileSpec.record_length.Should().Be(74);
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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

      DOSInterruptBtrieveCommand command = new DOSInterruptBtrieveCommand()
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
      _memory.GetWord(_registers.DS, (ushort) (_registers.DX + 4)).Should().Be(74);

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
      _memory.GetDWord(dataBuffer).Should().Be(4);
    }

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
  }
}