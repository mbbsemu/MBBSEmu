using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Telnet;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace MBBSEmu.DependencyInjection
{
    public static class ServiceResolver
    {
        private static readonly ServiceProvider Resolver;

        static ServiceResolver()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILogger>(LogManager.GetCurrentClassLogger(typeof(CustomLogger)));
            serviceCollection.AddSingleton<IMbbsHost, MbbsHost>();
            serviceCollection.AddSingleton<ITelnetServer, TelnetServer>();

            Resolver = serviceCollection.BuildServiceProvider();
        }

        public static T GetService<T>() => Resolver.GetService<T>();
    }
}
