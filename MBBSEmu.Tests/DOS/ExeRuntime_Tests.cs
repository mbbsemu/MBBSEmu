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
    public class ExeRuntime_Tests : TestBase, IDisposable
    {
        private readonly string[] _exeFiles = { "CMDLINE.EXE" };
        private readonly string[] _cmdLineArguments = {"one", "two", "three"};

        private string _modulePath;
        private ServiceResolver _serviceResolver;

        public ExeRuntime_Tests()
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
            foreach (var file in _exeFiles)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
            }
        }

        private static string GetExpectedOutput()
        {
            var expectedOutput = @"ds: 1703
es: 1000
cs: 1010
main: 1010:02A8
_environ: 0CDE
psp: 1000:0000
dta: 1000:0080

PSP: first byte beyond: 9FB3:0000
PSP: env_segment: 9FB4:0000
PSP: cmdTailLength: 13
PSP: env[0]:CMDLINE=C:\BBSV6\CMDLINE.EXE one two three
PSP: env[1]:COMSPEC=C:\COMMAND.COM
PSP: env[2]:COPYCMD=COPY
PSP: env[3]:DIRCMD=DIR
PSP: env[4]:PATH=C:\DOS;C:\BBSV6
PSP: env[5]:TMP=C:\TEMP
PSP: env[6]:TEMP=C:\TEMP
PSP: env[0]:0 env[1]:1 env[2]:0 env[3]:67 env[4]:58 env[5]:92

Printing 4 cmdline args from FFDC
FFE2:
FFF7:one
FFF7:two
FFF7:three

Printing environment variables from 0CDE
0C50:CMDLINE=C:\BBSV6\CMDLINE.EXE one two three
0C7B:COMSPEC=C:\COMMAND.COM
0C92:COPYCMD=COPY
0C9F:DIRCMD=DIR
0CAA:PATH=C:\DOS;C:\BBSV6
0CBF:TMP=C:\TEMP
0CCB:TEMP=C:\TEMP
";
            if (!expectedOutput.Contains("\r"))
                expectedOutput = expectedOutput.Replace("\n", "\r\n");

            return expectedOutput;
        }

        [Fact]
        public void haha()
        {
            var stdoutStream = new MemoryStream();
            var stdout = new StreamWriter(stdoutStream);
            var expectedOutput = GetExpectedOutput();

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            ExeRuntime exeRuntime = new ExeRuntime(
              new MZFile(Path.Combine(_modulePath, _exeFiles[0])),
              _serviceResolver.GetService<IClock>(),
              _serviceResolver.GetService<ILogger>(),
              _serviceResolver.GetService<IFileUtility>(),
              Console.In,
              stdout,
              stdout);

            exeRuntime.Load(_cmdLineArguments);
            exeRuntime.Run();

            stdout.Flush();
            stdoutStream.Seek(0, SeekOrigin.Begin);
            var output = Encoding.ASCII.GetString(stdoutStream.ToArray());

            output.Should().Be(expectedOutput);
        }
    }
}
