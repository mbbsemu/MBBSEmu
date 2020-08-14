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
using MBBSEmu.HostProcess.HostRoutines;
using MBBSEmu.Memory;

namespace MBBSEmu.DependencyInjection
{
    public class ServiceResolver
    {
        private static ServiceProvider _provider;
        private static IServiceCollection ServiceCollection;

        public const string DefaultAppSettingsFilename = "appsettings.json";

        // Following hack / workaround from [this Stack Overflow post](https://stackoverflow.com/questions/34219191/how-to-pass-parameter-to-static-class-constructor/34219280#34219280)
        // to move the guts of the Create() method from the static constructor they were in before.
        // Long term solution is probably to de-static this class :)
        private ServiceResolver() {}

        public static void Create(string appSettingsFilename = DefaultAppSettingsFilename)
        {
            //Prevent multiple creates
            if (_provider != null)
                return;

            ServiceCollection = new ServiceCollection();

            var ConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonStream(LoadAppSettings(appSettingsFilename))
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
            ServiceCollection.AddSingleton<IHostRoutines, MenuRoutines>();
            ServiceCollection.AddSingleton<IHostRoutines, FsdRoutines>();
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
        private static FileStream LoadAppSettings(string filename)
        {
            if(!File.Exists(filename))
                throw new FileNotFoundException($"Unable to locate [{filename}] emulator settings file.");

            if(!IsValidJson(File.ReadAllText(filename)))
                throw new InvalidDataException($"Invalid JSON detected in [{filename}]. Please verify the format & contents of the file are valid JSON.");

            return File.Open(filename, FileMode.Open, FileAccess.Read);
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
