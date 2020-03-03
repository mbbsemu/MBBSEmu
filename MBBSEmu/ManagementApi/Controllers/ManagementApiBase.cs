using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NLog;

namespace MBBSEmu.ManagementApi.Controllers
{
    public abstract class ManagementApiBase : ControllerBase
    {
        private protected static ILogger _logger => DependencyInjection.ServiceResolver.GetService<ILogger>();

        private protected IConfiguration Configuration { get; }

        protected ManagementApiBase(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
