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

        /// <summary>
        ///     Constructs the service provider.
        /// </summary>
        /// <param name="overrides">Any objects you want to bind instead of the defaults</param>
        public ServiceResolver(params object[] overrides)
        {
            BuildServiceProvider(overrides);
        }

        private void BuildServiceProvider(object[] overrides)
        {
            //Base Configuration Items
            AddSingleton<AppSettings>(overrides);
            AddSingleton<AppSettings>(overrides);
            AddSingleton<PointerDictionary<SessionBase>>(overrides);
            AddSingleton<IResourceManager, ResourceManager>(overrides);
            AddSingleton<IFileUtility, FileUtility>(overrides);
            _serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));

            //FSD Items
            AddSingleton<IGlobalCache, GlobalCache>(overrides);
            _serviceCollection.AddTransient<IFsdUtility, FsdUtility>();

            //Database Repositories
            AddSingleton<ISessionBuilder, SessionBuilder>(overrides);
            AddSingleton<IAccountRepository, AccountRepository>(overrides);
            AddSingleton<IAccountKeyRepository, AccountKeyRepository>(overrides);

            //MajorBBS Host Objects
            AddSingleton<IHostRoutine, MenuRoutines>(overrides);
            AddSingleton<IHostRoutine, FsdRoutines>(overrides);
            AddSingleton<IGlobalRoutine, UsersOnlineGlobal>(overrides);
            AddSingleton<IGlobalRoutine, PageUserGlobal>(overrides);
            AddSingleton<IGlobalRoutine, SysopGlobal>(overrides);
            AddSingleton<IMbbsHost, MbbsHost>(overrides);
            _serviceCollection.AddTransient<ISocketServer, SocketServer>();

            _provider = _serviceCollection.BuildServiceProvider();
        }

        private void AddSingleton<TService, TImplementation>(object[] overrides)
            where TService : class
            where TImplementation : class, TService
        {
            foreach (var obj in overrides)
            {
                if (obj is TService)
                {
                    _serviceCollection.AddSingleton<TService>((TService) obj);
                    return;
                }
            }

            _serviceCollection.AddSingleton<TService, TImplementation>();
        }

        private void AddSingleton<TServiceAndImplementation>(object[] overrides)
            where TServiceAndImplementation : class
        {
            foreach (var obj in overrides)
            {
                if (obj is TServiceAndImplementation)
                {
                    _serviceCollection.AddSingleton<TServiceAndImplementation>((TServiceAndImplementation) obj);
                    return;
                }
            }

            _serviceCollection.AddSingleton<TServiceAndImplementation>();
        }

        public void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public ServiceProvider GetServiceProvider() => _provider;

        public ServiceCollection GetServiceCollection() => _serviceCollection;

        public T GetService<T>() => _provider.GetService<T>();
    }
}
