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
        AccountModel GetAccountByUsernameAndPassword(string userName, string password);
    }
}
