using FluentAssertions;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS;
using MBBSEmu.IO;
using MBBSEmu.Resources;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text;
using MBBSEmu.Logging;
using Xunit;

namespace MBBSEmu.Tests.Memory
{
    [Collection("Non-Parallel")]
    public class BtrieveRuntime_Tests : TestBase, IDisposable
    {
        private readonly string[] _runtimeFiles = { "BTRIEVE.EXE", "MBBSEMU.DAT" };

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
            SqliteConnection.ClearAllPools();

            Directory.Delete(_modulePath, recursive: true);
        }

        private void CopyModuleToTempPath(IResourceManager resourceManager)
        {
            foreach (var file in _runtimeFiles)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
            }
        }

        private static string GetExpectedOutput(string datPath)
        {
            var expectedOutput = $"Successfully opened {datPath}!" + @"
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

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            ExeRuntime exeRuntime = new ExeRuntime(
              new MZFile(Path.Combine(_modulePath, _runtimeFiles[0])),
              _serviceResolver.GetService<IClock>(),
              _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>(),
              _serviceResolver.GetService<IFileUtility>(),
              _modulePath,
              null,
              new TextReaderStream(Console.In),
              stdout,
              stdout);

            exeRuntime.Load(new string[] {Path.Combine(_modulePath, _runtimeFiles[1])});
            exeRuntime.Run();

            stdout.Flush();
            stdoutStream.Seek(0, SeekOrigin.Begin);
            var output = Encoding.ASCII.GetString(stdoutStream.ToArray());

            output.Should().Be(GetExpectedOutput(Path.Combine(_modulePath, _runtimeFiles[1])));
        }
    }
}
