using FluentAssertions;
using MBBSEmu.IO;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    public class MsgFile_Tests : TestBase, IDisposable
    {
        private readonly string _modulePath;

        private MemoryStream Load(string resourceFile)
        {
            var resource = ResourceManager.GetTestResourceManager().GetResource($"MBBSEmu.Tests.Assets.{resourceFile}");
            return new MemoryStream(resource.ToArray());
        }

        public MsgFile_Tests()
        {
            _modulePath = GetModulePath();
        }

        public void Dispose()
        {
            if (Directory.Exists(_modulePath))
            {
                Directory.Delete(_modulePath, recursive: true);
            }
        }

        [Fact]
        public void ReplaceWithEmptyDictionary()
        {
            var sourceMessage = Load("MBBSEMU.MSG");
            var outputRawStream = new MemoryStream();
            using var sourceStream = new StreamStream(sourceMessage);
            using var outputStream = new StreamStream(outputRawStream);

            MsgFile.UpdateValues(sourceStream, outputStream, new Dictionary<string, string>());

            outputRawStream.Flush();
            outputRawStream.Seek(0, SeekOrigin.Begin);
            var result = outputRawStream.ToArray();

            sourceMessage.Seek(0, SeekOrigin.Begin);
            var expected = sourceMessage.ToArray();

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void ReplaceWithActualValues()
        {
            var sourceMessage = Load("MBBSEMU.MSG");
            var outputRawStream = new MemoryStream();
            using var sourceStream = new StreamStream(sourceMessage);
            using var outputStream = new StreamStream(outputRawStream);

            MsgFile.UpdateValues(sourceStream, outputStream, new Dictionary<string, string>() { { "SOCCCR", "128" }, { "SLOWTICS", "Whatever" }, { "MAXITEM", "45" } });

            outputRawStream.Flush();
            outputRawStream.Seek(0, SeekOrigin.Begin);
            var result = Encoding.ASCII.GetString(outputRawStream.ToArray());

            // expected should have the mods applied
            var expected = Encoding.ASCII.GetString(Load("MBBSEMU.MSG").ToArray());
            expected = expected.Replace("SOCCCR {SoC credit consumption rate adjustment, per min: 0}", "SOCCCR {SoC credit consumption rate adjustment, per min: 128}");
            expected = expected.Replace("SLOWTICS {Slow system factor: 10000}", "SLOWTICS {Slow system factor: Whatever}");
            expected = expected.Replace("MAXITEM {Maximum number of items: 954}", "MAXITEM {Maximum number of items: 45}");

            result.Should().Be(expected);
        }

        [Fact]
        public void ReplaceFileEmptyDictionary()
        {
            var fileName = Path.Combine(_modulePath, "MBBSEMU.MSG");

            Directory.CreateDirectory(_modulePath);
            File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());

            MsgFile.UpdateValues(fileName, new Dictionary<string, string>());

            File.ReadAllBytes(fileName).Should().BeEquivalentTo(Load("MBBSEMU.MSG").ToArray());
        }

        [Fact]
        public void ReplaceFileWithActualValues()
        {
            var fileName = Path.Combine(_modulePath, "MBBSEMU.MSG");

            Directory.CreateDirectory(_modulePath);
            File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());

            MsgFile.UpdateValues(fileName, new Dictionary<string, string>() { { "SOCCCR", "128" }, { "SLOWTICS", "Whatever" }, { "MAXITEM", "45" } });

            // expected should have the mods applied
            var expected = Encoding.ASCII.GetString(Load("MBBSEMU.MSG").ToArray());
            expected = expected.Replace("SOCCCR {SoC credit consumption rate adjustment, per min: 0}", "SOCCCR {SoC credit consumption rate adjustment, per min: 128}");
            expected = expected.Replace("SLOWTICS {Slow system factor: 10000}", "SLOWTICS {Slow system factor: Whatever}");
            expected = expected.Replace("MAXITEM {Maximum number of items: 954}", "MAXITEM {Maximum number of items: 45}");

            File.ReadAllBytes(fileName).Should().BeEquivalentTo(Encoding.ASCII.GetBytes(expected));
        }

        [Theory]
        [InlineData('\r', ' ')]
        [InlineData('~', '~')]
        public void ProcessValue_IgnoredValues(char currentCharacter, char previousCharacter)
        {
            var resultCharacter = MsgFile.ProcessValue(currentCharacter, previousCharacter, out var resultState);

            Assert.Equal(0, resultCharacter);
            Assert.Equal(MsgFile.MsgParseState.VALUE, resultState);
        }

        [Fact]
        public void ProcessValue_EscapedBracket()
        {
            var resultCharacter = MsgFile.ProcessValue('}', '~', out var resultState);

            Assert.Equal(0, resultCharacter);
            Assert.Equal(MsgFile.MsgParseState.ESCAPEBRACKET, resultState);
        }

        [Fact]
        public void ProcessValue_ClosingBracket()
        {
            var resultCharacter = MsgFile.ProcessValue('}', ' ', out var resultState);

            Assert.Equal(0, resultCharacter);
            Assert.Equal(MsgFile.MsgParseState.POSTVALUE, resultState);
        }

        [Theory]
        [InlineData('A', MsgFile.MsgParseState.KEY)]
        [InlineData('Z', MsgFile.MsgParseState.KEY)]
        [InlineData('1', MsgFile.MsgParseState.KEY)]
        [InlineData('0', MsgFile.MsgParseState.KEY)]
        [InlineData(' ', MsgFile.MsgParseState.PREKEY)]
        [InlineData('\r', MsgFile.MsgParseState.PREKEY)]
        [InlineData('\n', MsgFile.MsgParseState.PREKEY)]
        [InlineData('!', MsgFile.MsgParseState.PREKEY)]
        [InlineData('{', MsgFile.MsgParseState.PREKEY)]
        [InlineData('}', MsgFile.MsgParseState.PREKEY)]
        public void ProcessPreKey_Tests(char currentCharacter, MsgFile.MsgParseState expectedState)
        {
            var resultCharacter = MsgFile.ProcessPreKey(currentCharacter, out var resultState);

            Assert.Equal(currentCharacter, resultCharacter);
            Assert.Equal(expectedState, resultState);
        }

        [Theory]
        [InlineData('A', MsgFile.MsgParseState.KEY)]
        [InlineData('Z', MsgFile.MsgParseState.KEY)]
        [InlineData('1', MsgFile.MsgParseState.KEY)]
        [InlineData('0', MsgFile.MsgParseState.KEY)]
        [InlineData(' ', MsgFile.MsgParseState.POSTKEY)]
        [InlineData('\r', MsgFile.MsgParseState.POSTKEY)]
        [InlineData('\n', MsgFile.MsgParseState.POSTKEY)]
        [InlineData('!', MsgFile.MsgParseState.POSTKEY)]
        [InlineData('{', MsgFile.MsgParseState.POSTKEY)]
        [InlineData('}', MsgFile.MsgParseState.POSTKEY)]
        public void ProcessKey_Tests(char currentCharacter, MsgFile.MsgParseState expectedState)
        {
            var resultCharacter = MsgFile.ProcessKey(currentCharacter, out var resultState);

            Assert.Equal(currentCharacter, resultCharacter);
            Assert.Equal(expectedState, resultState);
        }

        [Theory]
        [InlineData('{', MsgFile.MsgParseState.VALUE)]
        [InlineData('\r', MsgFile.MsgParseState.POSTKEY)] //MajorMUD puts the key on its own line
        [InlineData('\n', MsgFile.MsgParseState.POSTKEY)] //MajorMUD puts the key on its own line
        [InlineData('A', MsgFile.MsgParseState.KEY)]
        [InlineData('Z', MsgFile.MsgParseState.KEY)]
        [InlineData('1', MsgFile.MsgParseState.KEY)]
        [InlineData('0', MsgFile.MsgParseState.KEY)]
        [InlineData(' ', MsgFile.MsgParseState.POSTKEY)]
        public void ProcessPostKey_Tests(char currentCharacter, MsgFile.MsgParseState expectedState)
        {
            var resultCharacter = MsgFile.ProcessPostKey(currentCharacter, out var resultState);

            Assert.Equal(currentCharacter, resultCharacter);
            Assert.Equal(expectedState, resultState);
        }

        [Theory]
        [InlineData('\n', MsgFile.MsgParseState.PREKEY)]
        [InlineData('A', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('Z', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('1', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('0', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData(' ', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('\r', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('!', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('{', MsgFile.MsgParseState.POSTVALUE)]
        [InlineData('}', MsgFile.MsgParseState.POSTVALUE)]
        public void ProcessPostValue_Tests(char currentCharacter, MsgFile.MsgParseState expectedState)
        {
            var resultCharacter = MsgFile.ProcessPostValue(currentCharacter, out var resultState);

            Assert.Equal(currentCharacter, resultCharacter);
            Assert.Equal(expectedState, resultState);
        }

        [Fact]
        public void LoadMsg_MajorMud()
        {
            var sourceMessage = Load("IntegrationTest.msg");

            var msgValues = MsgFile.ExtractMsgValues(sourceMessage.ToArray());

            Assert.Equal("This is topic 1: value\0", Encoding.ASCII.GetString(msgValues[4]));
            Assert.Equal("This is topic 2: value\0", Encoding.ASCII.GetString(msgValues[6]));
            Assert.Equal("This is topic 3: value\0", Encoding.ASCII.GetString(msgValues[7]));
            Assert.Equal("Escaped ~ Values } Test\0", Encoding.ASCII.GetString(msgValues[8]));
        }
    }
}
