using System.Linq;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.HostProcess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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


        [Route("users")]
        public IActionResult Users()
        {
            var userRepository = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();

            var users = userRepository.
            return Ok("Authorized");
        }
    }
}
