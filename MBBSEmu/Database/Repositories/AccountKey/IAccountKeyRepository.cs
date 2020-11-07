using System.Collections.Generic;
using MBBSEmu.Database.Repositories.AccountKey.Model;

namespace MBBSEmu.Database.Repositories.AccountKey
{
    public interface IAccountKeyRepository
    {
        bool CreateTable();
        bool TableExists();
        bool DropTable();
        bool InsertAccountKey(Model.AccountKeyModel accountKey);
        bool InsertAccountKey(int accountId, string accountKey);
        IEnumerable<AccountKeyModel> GetAccountKeysByAccountId(int accountId);
        IEnumerable<AccountKeyModel> GetAccountKeysByUsername(string username);
        bool InsertAccountKeyByUsername(string userName, string accountKey);
        void DeleteAccountKeyByUsernameAndAccountKey(string userName, string accountKey);
        public void Reset();
    }
}
