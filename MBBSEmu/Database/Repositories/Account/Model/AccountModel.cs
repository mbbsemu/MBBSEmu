using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Database.Repositories.Account.Model
{
    public class AccountModel
    {
        public ushort userId { get; set; }
        public string username { get; set; }
        public string passwordHash { get; set; }
        public string passwordSalt { get; set; }
        public string emailAddress { get; set; }
        public DateTime createDate { get; set; }
        public DateTime updateDate { get; set; }
    }
}
