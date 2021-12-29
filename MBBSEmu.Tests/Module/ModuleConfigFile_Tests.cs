using MBBSEmu.Converters;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    /// <summary>
    ///     Tests Proper Deserialization of a Modules Configuration JSON File
    /// </summary>
    public class ModuleConfigFile_Tests : TestBase
    {

        [Fact]
        public void Module_Single_NoPatch()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter() }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal("MBBSEMU", module.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu\", module.ModulePath);
            Assert.True(module.ModuleEnabled);
            Assert.Null(module.MenuOptionKey);
            Assert.Null(module.Patches);
        }

        [Fact]
        public void Module_Single_Patch()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter(), new JsonStringEnumConverter() }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_Patch.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal("MBBSEMU", module.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu\", module.ModulePath);
            Assert.False(module.ModuleEnabled);
            Assert.Null(module.MenuOptionKey);
            Assert.Single(module.Patches);

            var patch = module.Patches.First();
            Assert.Equal("Test Patch 1", patch.Name);
            Assert.Equal("Patch 1 for a Unit Test", patch.Description);
            Assert.Equal((uint)200, patch.AbsoluteOffset);
            Assert.Equal(ModulePatch.EnumModulePatchType.Hex, patch.PatchType);
            Assert.Equal("000102030405", patch.Patch);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, patch.GetBytes().ToArray());
        }

        [Fact]
        public void Module_Multiple_NoPatch()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter() }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Multiple_NoPatch.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Equal(2, result.Modules.Count);

            var module1 = result.Modules[0];
            Assert.Equal("MBBSEMU", module1.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu\", module1.ModulePath);
            Assert.True(module1.ModuleEnabled);
            Assert.Null(module1.MenuOptionKey);
            Assert.Null(module1.Patches);

            var module2 = result.Modules[1];
            Assert.Equal("MBBSEMU2", module2.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu2\", module2.ModulePath);
            Assert.False(module2.ModuleEnabled);
            Assert.Equal("A", module2.MenuOptionKey);
            Assert.Null(module2.Patches);
        }

        [Fact]
        public void Module_Multiple_Patch()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonBooleanConverter(), new JsonStringEnumConverter() }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Multiple_Patch.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Equal(2, result.Modules.Count);

            var module1 = result.Modules[0];
            Assert.Equal("MBBSEMU", module1.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu\", module1.ModulePath);
            Assert.True(module1.ModuleEnabled);
            Assert.Equal("1", module1.MenuOptionKey);
            Assert.Single(module1.Patches);

            var patch = module1.Patches.First();
            Assert.Equal("Test Patch 1", patch.Name);
            Assert.Equal("Patch 1 for a Unit Test", patch.Description);
            Assert.Equal((uint)200, patch.AbsoluteOffset);
            Assert.Equal(ModulePatch.EnumModulePatchType.Hex, patch.PatchType);
            Assert.Equal("000102030405", patch.Patch);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, patch.GetBytes().ToArray());

            var module2 = result.Modules[1];
            Assert.Equal("MBBSEMU2", module2.ModuleIdentifier);
            Assert.Equal(@"c:\dos\modules\mbbsemu2\", module2.ModulePath);
            Assert.False(module2.ModuleEnabled);
            Assert.Null(module2.MenuOptionKey);
            Assert.Single(module2.Patches);

            var patch2 = module2.Patches.First();
            Assert.Equal("Test Patch 2", patch2.Name);
            Assert.Equal("Patch 2 for a Unit Test", patch2.Description);
            Assert.Equal((uint)100, patch2.AbsoluteOffset);
            Assert.Equal(ModulePatch.EnumModulePatchType.Text, patch2.PatchType);
            Assert.Equal("TEST PATCH", patch2.Patch);
            Assert.Equal(Encoding.ASCII.GetBytes("TEST PATCH"), patch2.GetBytes().ToArray());
        }
    }
}
