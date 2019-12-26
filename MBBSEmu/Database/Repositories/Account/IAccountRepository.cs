namespace MBBSEmu.Database.Repositories.Account
{
    public interface IAccountRepository
    {
        bool CreateTable();
        bool TableExists();
        bool DropTable();
        bool InsertAccount(string username, string plaintextPassword, string email);
    }
}
