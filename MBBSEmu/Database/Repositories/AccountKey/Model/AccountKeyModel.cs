using System;

namespace MBBSEmu.Database.Repositories.AccountKey.Model
{
    public class AccountKeyModel
    {
        public int accountKeyId { get; set; }
        public int accountId { get; set; }
        public string accountKey { get; set; }
        public DateTime createDate { get; set; }
        public DateTime updateDate { get; set; }
    }
}
