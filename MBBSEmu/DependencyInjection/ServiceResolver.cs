using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Session;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Telnet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System.IO;

namespace MBBSEmu.DependencyInjection
{
    public static class ServiceResolver
    {
        private static ServiceProvider _provider;

        static ServiceResolver()
        {
            //Build Configuration 
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IConfigurationRoot>(configuration);
            serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            serviceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            serviceCollection.AddSingleton<ITelnetServer, TelnetServer>();
            serviceCollection.AddSingleton<ISessionBuilder, SessionBuilder>();
            serviceCollection.AddSingleton<IAccountRepository, AccountRepository>();
            _provider = serviceCollection.BuildServiceProvider();
        }

        public static void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public static T GetService<T>() => _provider.GetService<T>();
    }
}
