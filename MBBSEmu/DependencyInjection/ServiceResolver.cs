using System;
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
using Microsoft.Extensions.DependencyInjection;
using NLog;
using MBBSEmu.HostProcess.GlobalRoutines;
using MBBSEmu.Session;

namespace MBBSEmu.DependencyInjection
{
    public class ServiceResolver : IDisposable
    {
        private ServiceProvider _provider;
        private readonly ServiceCollection _serviceCollection = new ServiceCollection();

        public void Dispose()
        {
            _provider.Dispose();
        }

        public ServiceResolver()
        {
            BuildServiceProvider();
        }

        private void BuildServiceProvider()
        {
            //Base Configuration Items
            _serviceCollection.AddSingleton<AppSettings>();
            _serviceCollection.AddSingleton<PointerDictionary<SessionBase>>();
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
