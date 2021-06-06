using System.Collections.Generic;
using MBBSEmu.Database.Repositories.Account.Model;

namespace MBBSEmu.Database.Repositories.Account
{
    public interface IAccountRepository
    {
        bool CreateTable();
        bool TableExists();
        bool DropTable();
        int InsertAccount(string userName, string plaintextPassword, string email, string sex);
        AccountModel GetAccountByUsername(string userName);
        AccountModel GetAccountByEmail(string email);
        AccountModel GetAccountByUsernameAndPassword(string userName, string password);
        IEnumerable<AccountModel> GetAccounts();
        AccountModel GetAccountById(int accountId);
        void DeleteAccountById(int accountId);
        void UpdateAccountById(int accountId, string userName, string plaintextPassword, string email, string sex);
        void Reset(string sysopPassword);
    }
}
