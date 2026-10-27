using FluentAssertions;
using MBBSEmu.Converters;
using MBBSEmu.Module;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    public class ModuleListFile_Tests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"mbbsemu-modulelist-{Guid.NewGuid():N}");
        private readonly string _file;
        private readonly string _modulePath;

        /// <summary>
        ///     A hand-written module list using every feature of the format
        /// </summary>
        private const string HandWritten = """
            {
              "BasePath": ".",
              "Modules": [
                {
                  "Identifier": "GWWARROW",
                  "Path": "GWWARROW",
                  "MenuOptionKey": "Q",
                  "Enabled": 0,
                  "Patches": [
                    {
                      "Name": "Test Patch 1",
                      "Description": "Patch 1 for a Unit Test",
                      "AbsoluteOffset": 200,
                      "PatchType": "Hex",
                      "Patch": "000102030405"
                    }
                  ]
                }
              ]
            }
            """;

        public ModuleListFile_Tests()
        {
            Directory.CreateDirectory(_folder);
            _file = Path.Combine(_folder, ModuleListFile.DefaultFileName);
            _modulePath = Path.Combine(_folder, "modules", "MJWMUT");
        }

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        /// <summary>
        ///     Reads a module list the way -C does
        /// </summary>
        private static ModuleConfigurationFile ReadWithModuleConfigurationReader(string path) =>
            JsonSerializer.Deserialize<ModuleConfigurationFile>(File.ReadAllText(path), new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonModuleConfigurationFileConverter(),
                    new JsonBooleanConverter(),
                    new JsonStringEnumConverter(),
                    new JsonFarPtrConverter()
                }
            });

        [Fact]
        public void Append_NoFile_CreatesModuleList()
        {
            ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");

            var root = JsonNode.Parse(File.ReadAllText(_file))!.AsObject();
            root.Count.Should().Be(1, "only Modules is written");
            root.ContainsKey("BasePath").Should().BeFalse();
            var modules = root["Modules"]!.AsArray();
            modules.Should().HaveCount(1);
            JsonNode.DeepEquals(modules[0], new JsonObject
            {
                ["Identifier"] = "MJWMUT",
                ["Path"] = _modulePath,
                ["MenuOptionKey"] = "A"
            }).Should().BeTrue(modules[0]!.ToJsonString());
        }

        [Fact]
        public void Append_WrittenFile_LoadsWithTheModuleConfigurationReader()
        {
            ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");

            var module = ReadWithModuleConfigurationReader(_file).Modules.Should().ContainSingle().Subject;
            module.ModuleIdentifier.Should().Be("MJWMUT");
            module.ModulePath.Should().Be(_modulePath);
            module.MenuOptionKey.Should().Be("A");
        }

        [Fact]
        public void Append_HandWrittenFile_KeepsEverythingAlreadyThere()
        {
            File.WriteAllText(_file, HandWritten);
            var original = JsonNode.Parse(HandWritten)!;

            ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");

            var root = JsonNode.Parse(File.ReadAllText(_file))!;
            root["BasePath"]!.GetValue<string>().Should().Be(".");
            var modules = root["Modules"]!.AsArray();
            modules.Should().HaveCount(2);
            JsonNode.DeepEquals(modules[0], original["Modules"]![0]).Should().BeTrue(modules[0]!.ToJsonString());
            modules[1]!["Identifier"]!.GetValue<string>().Should().Be("MJWMUT");

            //The appended path is absolute, so the BasePath isn't combined with it
            ReadWithModuleConfigurationReader(_file).Modules[1].ModulePath.Should().Be(_modulePath);
        }

        [Fact]
        public void Append_InvalidJson_ThrowsAndLeavesFileUnchanged()
        {
            File.WriteAllText(_file, "{ not json");
            var before = File.ReadAllBytes(_file);

            var append = () => ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");

            append.Should().Throw<InvalidDataException>();
            File.ReadAllBytes(_file).Should().Equal(before);
        }

        [Fact]
        public void Append_ModulesNotAnArray_Throws()
        {
            File.WriteAllText(_file, """{"Modules":{}}""");

            var append = () => ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");

            append.Should().Throw<InvalidDataException>();
        }

        [Fact]
        public void Append_LeavesNoTemporaryFile()
        {
            ModuleListFile.Append(_file, "MJWMUT", _modulePath, "A");
            ModuleListFile.Append(_file, "GWWARROW", Path.Combine(_folder, "GWWARROW"), "B");

            Directory.GetFiles(_folder).Should().Equal(_file);
            JsonNode.Parse(File.ReadAllText(_file))!["Modules"]!.AsArray().Should().HaveCount(2);
        }
    }
}
