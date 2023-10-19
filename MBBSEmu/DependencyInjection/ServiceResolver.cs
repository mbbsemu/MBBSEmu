using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Fsd;
using MBBSEmu.HostProcess.GlobalRoutines;
using MBBSEmu.HostProcess.HostRoutines;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Resources;
using MBBSEmu.Server.Socket;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
using MBBSEmu.Util;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace MBBSEmu.DependencyInjection
{
    public class ServiceResolver : IDisposable
    {
        private ServiceProvider _provider;
        private readonly ServiceCollection _serviceCollection = new();

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

        /// <summary>
        ///     
        /// </summary>
        /// <param name="overrides"></param>
        private void BuildServiceProvider(IEnumerable<object> overrides)
        {
            //Base Configuration Items
            AddSingleton<LogFactory>(overrides);
            AddSingleton<AppSettingsManager>(overrides);
            AddSingleton<PointerDictionary<SessionBase>>(overrides);
            AddSingleton<IResourceManager, ResourceManager>(overrides);
            AddSingleton<IFileUtility, FileUtility>(overrides);

            //FSD Items
            AddSingleton<IGlobalCache, GlobalCache>(overrides);
            _serviceCollection.AddTransient<IFsdUtility, FsdUtility>();

            //Database Repositories
            AddSingleton<ISessionBuilder, SessionBuilder>(overrides);
            AddSingleton<IAccountRepository, AccountRepository>(overrides);
            AddSingleton<IAccountKeyRepository, AccountKeyRepository>(overrides);

            //MajorBBS Host Objects
            AddSingleton<ITextVariableService, TextVariableService>(overrides);
            AddSingleton<IHostRoutine, MenuRoutines>(overrides);
            AddSingleton<IHostRoutine, FsdRoutines>(overrides);
            AddSingleton<IGlobalRoutine, UsersOnlineGlobal>(overrides);
            AddSingleton<IGlobalRoutine, PageUserGlobal>(overrides);
            AddSingleton<IGlobalRoutine, SysopGlobal>(overrides);
            AddSingleton<IMbbsHost, MbbsHost>(overrides);
            AddSingleton<IMessagingCenter, MessagingCenter>(overrides);
            _serviceCollection.AddTransient<ISocketServer, SocketServer>();

            //System clock
            AddSingleton<IClock, SystemClock>(overrides);

            _provider = _serviceCollection.BuildServiceProvider();
        }

        private void AddSingleton<TService, TImplementation>(IEnumerable<object> overrides)
            where TService : class
            where TImplementation : class, TService
        {
            foreach (var obj in overrides)
            {
                if (obj is TService service)
                {
                    _serviceCollection.AddSingleton(service);
                    return;
                }
            }

            _serviceCollection.AddSingleton<TService, TImplementation>();
        }

        private void AddSingleton<TServiceAndImplementation>(IEnumerable<object> overrides)
            where TServiceAndImplementation : class
        {
            foreach (var obj in overrides)
            {
                if (obj is TServiceAndImplementation service)
                {
                    _serviceCollection.AddSingleton(service);
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
