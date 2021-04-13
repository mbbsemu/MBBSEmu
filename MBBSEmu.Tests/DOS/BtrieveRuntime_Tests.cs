using FluentAssertions;
using MBBSEmu.CPU;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.DOS;
using MBBSEmu.Extensions;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Resources;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    public class BtrieveRuntime_Tests : TestBase, IDisposable
    {
        private readonly string[] _runtimeFiles = { "BTRIEVE.EXE", "MBBSEMU.DAT" };
        private readonly string[] _cmdLineArguments = {"MBBSEMU.DAT"};

        private string _modulePath;
        private ServiceResolver _serviceResolver;

        public BtrieveRuntime_Tests()
        {
            _modulePath = GetModulePath();

            _serviceResolver = new ServiceResolver(SessionBuilder.ForTest($"MBBSExeRuntime_{RANDOM.Next()}"));

            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, recursive: true);
        }

        private void CopyModuleToTempPath(IResourceManager resourceManager)
        {
            foreach (var file in _runtimeFiles)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
            }
        }

        private static string GetExpectedOutput()
        {
            var expectedOutput = @"Successfully opened MBBSEMU.DAT!
record_length:     74
page_size:         512
number_of_keys:    4
number_of_records: 4
flags:             0x0

key0_position:  3
key0_length:    32
key0_flags:     0x101
key0_data_type: 11
key1_position:  35
key1_length:    4
key1_flags:     0x102
key1_data_type: 1
key2_position:  39
key2_length:    32
key2_flags:     0x103
key2_data_type: 11
key3_position:  71
key3_length:    4
key3_flags:     0x100
key3_data_type: 15
";
            if (!expectedOutput.Contains("\r"))
                expectedOutput = expectedOutput.Replace("\n", "\r\n");

            return expectedOutput;
        }

        [Fact]
        public void BTRIEVE_EXE()
        {
            var stdoutStream = new MemoryStream();
            var stdout = new TextWriterStream(new StreamWriter(stdoutStream));
            var expectedOutput = GetExpectedOutput();

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            //Directory.SetCurrentDirectory(_modulePath);

            ExeRuntime exeRuntime = new ExeRuntime(
              new MZFile(Path.Combine(_modulePath, _runtimeFiles[0])),
              _serviceResolver.GetService<IClock>(),
              _serviceResolver.GetService<ILogger>(),
              _serviceResolver.GetService<IFileUtility>(),
              null,
              new TextReaderStream(Console.In),
              stdout,
              stdout);

            exeRuntime.Load(_cmdLineArguments);
            exeRuntime.Run();

            stdout.Flush();
            stdoutStream.Seek(0, SeekOrigin.Begin);
            var output = Encoding.ASCII.GetString(stdoutStream.ToArray());

            //output.Should().Be(expectedOutput);
        }
    }
}
