using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Telnet;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace MBBSEmu.DependencyInjection
{
    public static class ServiceResolver
    {
        private static ServiceProvider _provider;

        static ServiceResolver()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            serviceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            serviceCollection.AddSingleton<ITelnetServer, TelnetServer>();

            _provider = serviceCollection.BuildServiceProvider();
        }

        public static void SetServiceProvider(ServiceProvider serviceProvider) => _provider = serviceProvider;

        public static T GetService<T>() => _provider.GetService<T>();
    }
}
