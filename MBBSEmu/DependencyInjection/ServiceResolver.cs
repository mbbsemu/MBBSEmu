using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.HostProcess.HostRoutines;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Resources;
using MBBSEmu.Server.Socket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.Collections.Generic;
using MBBSEmu.HostProcess.GlobalRoutines;

namespace MBBSEmu.DependencyInjection
{
    public class ServiceResolver
    {
        private ServiceProvider _provider;
        private readonly ServiceCollection _serviceCollection = new ServiceCollection();

        public ServiceResolver(IEnumerable<KeyValuePair<string,string>> data) {
            var configurationRoot = new ConfigurationBuilder()
                .AddInMemoryCollection(data)
                .Build();

            BuildServiceProvider(configurationRoot);
        }

        public ServiceResolver(IConfigurationRoot configurationRoot)
        {
            BuildServiceProvider(configurationRoot);
        }

        public static List<KeyValuePair<string, string>> GetTestDefaults()
        {
            return new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("BBS.Title", "Test"),
                new KeyValuePair<string, string>("GSBL.Activation", "123456789"),
                new KeyValuePair<string, string>("Telnet.Enabled", "False"),
                new KeyValuePair<string, string>("Rlogin.Enabled", "False"),
                new KeyValuePair<string, string>("Database.File", "mbbsemu.db")
            };
        }

        private void BuildServiceProvider(IConfigurationRoot configurationRoot)
        {
            //Base Configuration Items
            _serviceCollection.AddSingleton<IConfiguration>(configurationRoot);
            _serviceCollection.AddSingleton<IResourceManager, ResourceManager>();
            _serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            _serviceCollection.AddSingleton<IFileUtility, FileUtility>();

            //FSD Items
            _serviceCollection.AddSingleton<IGlobalCache, GlobalCache>();
            _serviceCollection.AddTransient<IFsdUtility, FsdUtility>();

            //Database Repositories
            _serviceCollection.AddSingleton<ISessionBuilder, SessionBuilder>();
            _serviceCollection.AddSingleton<IAccountRepository, AccountRepository>();
            _serviceCollection.AddSingleton<IAccountKeyRepository, AccountKeyRepository>();

            //MajorBBS Host Objects
            _serviceCollection.AddSingleton<IHostRoutine, MenuRoutines>();
            _serviceCollection.AddSingleton<IHostRoutine, FsdRoutines>();
            _serviceCollection.AddSingleton<IGlobalRoutine, UsersOnlineGlobal>();
            _serviceCollection.AddSingleton<IGlobalRoutine, PageUserGlobal>();
            _serviceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            _serviceCollection.AddTransient<ISocketServer, SocketServer>();

            _provider = _serviceCollection.BuildServiceProvider();
        }

        public void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public ServiceProvider GetServiceProvider() => _provider;

        public ServiceCollection GetServiceCollection() => _serviceCollection;

        public T GetService<T>() => _provider.GetService<T>();
    }
}
