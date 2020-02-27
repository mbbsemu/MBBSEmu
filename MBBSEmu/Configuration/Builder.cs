using Microsoft.Extensions.Configuration;
using System.IO;

namespace MBBSEmu.Configuration
{
    public static class Builder
    {
        public static IConfigurationRoot ConfigurationRoot;

        public static void Build(string configFile)
        {
            //Build Configuration 
            ConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configFile, optional: false, reloadOnChange: true).Build();
        }
    }
}
