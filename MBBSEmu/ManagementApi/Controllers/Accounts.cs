using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.ManagementApi.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace MBBSEmu.ManagementApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class Accounts : ManagementApiBase
    {
        private readonly IAccountRepository _accountRepository;

        public Accounts(IConfiguration configuration) : base(configuration)
        {
            _accountRepository = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();
        }

        [HttpGet]
        public IActionResult Get()
        {
            var totalUsers = _accountRepository.GetAccounts();

            var result = totalUsers.Select(x => new
            {
                accountId = x.accountId,
                userName = x.userName
            });

            return Ok(result);
        }

        [HttpGet]
        [Route("{accountId}")]
        public IActionResult GetAccount(int accountId)
        {
            var result = _accountRepository.GetAccountById(accountId);

            return Ok(new
            {
                result.accountId,
                result.userName,
                result.email,
                result.createDate,
                result.updateDate
            });
        }

        [HttpGet]
        [Route("delete/{accountId}")]
        public IActionResult DeleteAccount(int accountId)
        {
            _accountRepository.DeleteAccountById(accountId);

            return Ok();
        }

        [HttpPost]
        [Route("create")]
        public IActionResult CreateAccount([FromBody] AccountDTO account)
        {
            //Validate
            if (string.IsNullOrEmpty(account.userName) || string.IsNullOrEmpty(account.password) || string.IsNullOrEmpty(account.email))
                return BadRequest();

            var result = _accountRepository.InsertAccount(account.userName, account.password, account.email);

            return Ok();
        }

        [HttpPost]
        [Route("update/{accountId}")]
        public IActionResult UpdateAccount(int accountId, [FromBody] AccountDTO account)
        {
            //Validate
            if (string.IsNullOrEmpty(account.userName) || string.IsNullOrEmpty(account.password) || string.IsNullOrEmpty(account.email))
                return BadRequest();

            _accountRepository.UpdateAccountById(accountId, account.userName, account.password, account.email);

            return Ok();
        }
    }
}
