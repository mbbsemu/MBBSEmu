using System.Net;
using System.Threading;
using MBBSEmu.ManagementApi.Kestrel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NLog;

namespace MBBSEmu.ManagementApi
{
    public class ApiHost : IApiHost
    {
        private readonly ILogger _logger;

        private readonly IWebHost _apiHost;
        private readonly Thread _apiHostThread;

        public ApiHost(IConfiguration configuration, ILogger logger)
        {
            _logger = logger;
            _apiHost = new WebHostBuilder()
                .UseKestrel(options => { options.Listen(IPAddress.Any, 8080); }).UseConfiguration(configuration)
                .UseStartup<Startup>().Build();

            _apiHostThread = new Thread(HostThread);
        }

        private void HostThread()
        {
            _logger.Info($"Started Management API");
            _apiHost.Run();
        }

        public void Start()
        {
            _logger.Info($"Starting Management API...");
            _apiHostThread.Start();
        }

        public void Stop()
        {
            _apiHostThread.Abort();
            _logger.Info($"Stopped Management API");
        }
    }
}
