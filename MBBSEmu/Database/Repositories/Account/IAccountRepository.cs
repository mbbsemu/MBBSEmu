using System.Collections.Generic;
using MBBSEmu.Database.Repositories.Account.Model;

namespace MBBSEmu.Database.Repositories.Account
{
    public interface IAccountRepository
    {
        bool CreateTable();
        bool TableExists();
        bool DropTable();
        int InsertAccount(string userName, string plaintextPassword, string email);
        AccountModel GetAccountByUsername(string userName);
        AccountModel GetAccountByEmail(string email);
        AccountModel GetAccountByUsernameAndPassword(string userName, string password);
        IEnumerable<AccountModel> GetAccounts();
    }
}
