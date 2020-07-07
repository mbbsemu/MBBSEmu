using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.HostProcess;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Resources;
using MBBSEmu.Server.Socket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.IO;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.Memory;

namespace MBBSEmu.DependencyInjection
{
    public static class ServiceResolver
    {
        private static ServiceProvider _provider;
        private static readonly IServiceCollection ServiceCollection;

        static ServiceResolver()
        {
            ServiceCollection = new ServiceCollection();

            var ConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonStream(LoadAppSettings())
                .Build();

            //Base Configuration Items
            ServiceCollection.AddSingleton<IConfiguration>(ConfigurationRoot);
            ServiceCollection.AddSingleton<IResourceManager, ResourceManager>();
            ServiceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            ServiceCollection.AddSingleton<IFileUtility, FileUtility>();

            //FSD Items
            ServiceCollection.AddSingleton<IGlobalCache, GlobalCache>();
            ServiceCollection.AddTransient<IFsdUtility, FsdUtility>();

            //Database Repositories
            ServiceCollection.AddSingleton<ISessionBuilder, SessionBuilder>();
            ServiceCollection.AddSingleton<IAccountRepository, AccountRepository>();
            ServiceCollection.AddSingleton<IAccountKeyRepository, AccountKeyRepository>();

            //MajorBBS Host Objects
            ServiceCollection.AddSingleton<IMbbsRoutines, MbbsRoutines>();
            ServiceCollection.AddSingleton<IMbbsRoutines, FsdRoutines>();
            ServiceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            ServiceCollection.AddTransient<ISocketServer, SocketServer>();

            _provider = ServiceCollection.BuildServiceProvider();
        }

        public static void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public static ServiceProvider GetServiceProvider() => _provider;

        public static IServiceCollection GetServiceCollection() => ServiceCollection;

        public static T GetService<T>() => _provider.GetService<T>();

        /// <summary>
        ///     Safe loading of appsettings.json for Configuration Builder
        /// </summary>
        /// <returns></returns>
        private static FileStream LoadAppSettings()
        {
            if(!File.Exists("appsettings.json"))
                throw new FileNotFoundException("Unable to locate appsettings.json file. Please ensure the file is in the same directory as the MBBSEmu executable file.");

            if(!IsValidJson(File.ReadAllText("appsettings.json")))
                throw new InvalidDataException("Invalid JSON detected in appsettings.json. Please verify the format & contents of the file are valid JSON.");

            return File.Open("appsettings.json", FileMode.Open, FileAccess.Read);
        }

        /// <summary>
        ///     Validates that a JSON file is a correct Format
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns></returns>
        private static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if (strInput.StartsWith("{") && strInput.EndsWith("}") || //For object
                strInput.StartsWith("[") && strInput.EndsWith("]")) //For array
            {
                try
                {
                    JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    //Exception in parsing json
                    Console.WriteLine($"JSON Parsing Error: {jex.Message}");
                    return false;
                }
                catch (Exception ex) //some other exception
                {
                    Console.WriteLine($"JSON Parsing Exception: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
