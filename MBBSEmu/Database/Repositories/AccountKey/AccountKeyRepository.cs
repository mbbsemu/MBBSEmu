using MBBSEmu.Database.Repositories.AccountKey.Model;
using MBBSEmu.Database.Repositories.AccountKey.Queries;
using MBBSEmu.Database.Session;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.Database.Repositories.AccountKey
{
    public class AccountKeyRepository : RepositoryBase, IAccountKeyRepository
    {
        public AccountKeyRepository(ISessionBuilder sessionBuilder) : base(sessionBuilder)
        {
        }

        public bool CreateTable()
        {
            var result = Query(EnumQueries.CreateAccountKeysTable, null);
            return true;
        }

        public bool TableExists()
        {
            var result = Query(EnumQueries.AccountKeysTableExists, null);
            return result.Any();
        }

        public bool DropTable()
        {
            var result = Query(EnumQueries.DropAccountKeysTable, null);
            return result.Any();
        }

        public bool InsertAccountKey(Model.AccountKeyModel accountKey) =>
            InsertAccountKey(accountKey.accountId, accountKey.accountKey);

        public bool InsertAccountKey(int accountId, string accountKey)
        {
            var result = Query(EnumQueries.InsertAccountKey, new {accountId, accountKey});
            return result.Any();
        }

        public IEnumerable<AccountKeyModel> GetAccountKeysByAccountId(int accountId)
        {
            return Query<AccountKeyModel>(EnumQueries.GetAccountKeysByAccountId, new { accountId });
        }
    }
}
