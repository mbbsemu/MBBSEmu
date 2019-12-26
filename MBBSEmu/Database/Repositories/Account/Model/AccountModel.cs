using System;

namespace MBBSEmu.Database.Repositories.Account.Model
{
    public class AccountModel
    {
        public ushort userId { get; set; }
        public string userName { get; set; }
        public string passwordHash { get; set; }
        public string passwordSalt { get; set; }
        public string email { get; set; }
        public string userKey { get; set; }
        public DateTime createDate { get; set; }
        public DateTime updateDate { get; set; }
    }
}
