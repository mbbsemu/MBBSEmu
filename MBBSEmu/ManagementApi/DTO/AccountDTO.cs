using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.ManagementApi.DTO
{
    public class AccountDTO
    {
        public int accountId { get; set; }
        public string userName { get; set; }
        public string password { get; set; }
        public string email { get; set; }
    }
}
