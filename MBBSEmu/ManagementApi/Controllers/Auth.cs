using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;

namespace MBBSEmu.ManagementApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class Auth : ManagementApiBase
    {
        public Auth(IConfiguration configuration) : base(configuration)
        {
            
        }

        [HttpGet]
        public IActionResult Get(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Unauthorized();
            
            //Verify the User Credentials
            var userRepository = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();
            var user = userRepository.GetAccountByUsernameAndPassword(username, password);
            if (user == null)
                return Unauthorized();

            //Verify the User has SYSOP access
            var userKeysRepository = DependencyInjection.ServiceResolver.GetService<IAccountKeyRepository>();
            var userKeys = userKeysRepository.GetAccountKeysByAccountId(user.accountId);
            if (userKeys == null || userKeys.Count(x => x.accountKey == "SYSOP") == 0)
                return Unauthorized();

            //Generate JWT Token
            var key = Configuration["ManagementAPI.Secret"]; //Secret key which will be used later during validation    
            var issuer = "MBBSEmu Management API";  //normally this will be your site URL    

            var securityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Create Security Token object by giving required parameters    
            var token = new JwtSecurityToken(issuer, //Issure    
                issuer,  //Audience    
                null,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials);

            var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new {token = jwtToken });
        }
    }
}
