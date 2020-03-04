using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.HostProcess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace MBBSEmu.ManagementApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class Stats : ManagementApiBase
    {

        public Stats(IConfiguration configuration) : base(configuration)
        {
            
        }

        [HttpGet]
        [Route("users/online")]
        public IActionResult UsersOnline()
        {
            var mbbsHost = DependencyInjection.ServiceResolver.GetService<IMbbsHost>();

            var usersOnline = mbbsHost.GetUserSessions().Select(x => new
            {
                channel = x.Channel, 
                userName = x.Username, 
                module = x.CurrentModule?.ModuleIdentifier,
                sessionState = x.SessionState.ToString(), 
                sessionTime = x.SessionTimer.Elapsed.TotalSeconds
            });
            return Ok(usersOnline);
        }

        [HttpGet]
        [Route("users")]
        public IActionResult Users()
        {
            var userRepository = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();

            var totalUsers = userRepository.GetAccounts();

            var userStats = new
            {
                total = totalUsers.Count()
            };
            return Ok(userStats);
        }
    }
}
