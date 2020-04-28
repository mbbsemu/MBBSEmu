using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.HostProcess;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.ManagementApi;
using MBBSEmu.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;
using MBBSEmu.Server.Socket;

namespace MBBSEmu.DependencyInjection
{
    public static class ServiceResolver
    {
        private static ServiceProvider _provider;
        private static IServiceCollection _serviceCollection;

        static ServiceResolver()
        {
            _serviceCollection = new ServiceCollection();

            var ConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();

            //Base Configuration Items
            _serviceCollection.AddSingleton<IConfiguration>(ConfigurationRoot);
            _serviceCollection.AddSingleton<IResourceManager, ResourceManager>();
            _serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            _serviceCollection.AddSingleton<IFileUtility, FileUtility>();

            //Database Repositories
            _serviceCollection.AddSingleton<ISessionBuilder, SessionBuilder>();
            _serviceCollection.AddSingleton<IAccountRepository, AccountRepository>();
            _serviceCollection.AddSingleton<IAccountKeyRepository, AccountKeyRepository>();

            //MajorBBS Host Objects
            _serviceCollection.AddSingleton<IMbbsRoutines, MbbsRoutines>();
            _serviceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            _serviceCollection.AddTransient<ISocketServer, SocketServer>();

            //API Host Objects
            _serviceCollection.AddSingleton<IApiHost, ApiHost>();

            _provider = _serviceCollection.BuildServiceProvider();
        }

        public static void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public static ServiceProvider GetServiceProvider() => _provider;

        public static IServiceCollection GetServiceCollection() => _serviceCollection;

        public static T GetService<T>() => _provider.GetService<T>();
    }
}
