using MBBSEmu.Converters;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System.Text.Json;
using Xunit;

namespace MBBSEmu.Tests.Converters
{
    public class JsonModuleConfigurationFileConverter_Tests : TestBase
    {
        [Fact]
        public void Module_Single_NoPatch_BasePath()
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonModuleConfigurationFileConverter(),
                    new JsonBooleanConverter()
                }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal(@"c:\dos\modules\mbbsemu\", module.ModulePath);
        }

        [Fact]
        public void Module_Single_NoPatch_BasePath_NoConverter()
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonBooleanConverter()
                }
            };

            var resourceManager = ResourceManager.GetTestResourceManager();

            var jsonToDeserialize = resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath.json");

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal(@"modules\mbbsemu\", module.ModulePath);
        }
    }
}
