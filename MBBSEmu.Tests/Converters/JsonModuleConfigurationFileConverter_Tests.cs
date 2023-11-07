using MBBSEmu.Converters;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using System;
using System.Text.Json;
using Xunit;

namespace MBBSEmu.Tests.Converters
{
    public class JsonModuleConfigurationFileConverter_Tests : TestBase
    {
        [Fact]
        public void Module_Single_NoPatch_BasePath()
        {
            var resourceManager = ResourceManager.GetTestResourceManager();
            var jsonToDeserialize = string.Empty;
            var expectedPath = "";

            //Determine Platform and using a switch, set the file to be opened using resourceManager and the resulting BasePath
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32S:
                case PlatformID.WinCE:
                case PlatformID.Win32NT:

                    jsonToDeserialize =
                        resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath_Windows.json");
                    expectedPath = @"c:\dos\modules\mbbsemu\";
                    break;
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    jsonToDeserialize =
                        resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath_Linux.json");
                    expectedPath = "/dos/modules/mbbsemu/";
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }


            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonModuleConfigurationFileConverter(),
                    new JsonBooleanConverter()
                }
            };

            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal(expectedPath, module.ModulePath);
        }

        [Fact]
        public void Module_Single_NoPatch_BasePath_NoConverter()
        {
            var resourceManager = ResourceManager.GetTestResourceManager();
            var jsonToDeserialize = string.Empty;
            var expectedPath = "";

            //Determine Platform and using a switch, set the file to be opened using resourceManager and the resulting BasePath
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32S:
                case PlatformID.WinCE:
                case PlatformID.Win32NT:

                    jsonToDeserialize =
                        resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath_Windows.json");
                    expectedPath = @"modules\mbbsemu\";
                    break;
                case PlatformID.MacOSX:
                case PlatformID.Unix:
                    jsonToDeserialize =
                        resourceManager.GetString("MBBSEmu.Tests.Assets.Module_Single_NoPatch_BasePath_Linux.json");
                    expectedPath = "modules/mbbsemu/";
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonBooleanConverter()
                }
            };


            var result = JsonSerializer.Deserialize<ModuleConfigurationFile>(jsonToDeserialize, options);
            Assert.NotNull(result);
            Assert.Single(result.Modules);

            var module = result.Modules[0];
            Assert.Equal(expectedPath, module.ModulePath);
        }
    }
}
