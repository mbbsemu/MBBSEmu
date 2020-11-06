using MBBSEmu.Database.Repositories.AccountKey.Model;
using MBBSEmu.Database.Repositories.AccountKey.Queries;
using MBBSEmu.Database.Session;
using System.Collections.Generic;
using System.Linq;
using MBBSEmu.Resources;

namespace MBBSEmu.Database.Repositories.AccountKey
{
    public class AccountKeyRepository : RepositoryBase, IAccountKeyRepository
    {
        public AccountKeyRepository(ISessionBuilder sessionBuilder, IResourceManager resourceManager) : base(sessionBuilder, resourceManager)
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

        public IEnumerable<AccountKeyModel> GetAccountKeysByUsername(string userName)
        {
            return Query<AccountKeyModel>(EnumQueries.GetAccountKeysByUsername, new {userName});
        }

        public bool InsertAccountKeyByUsername(string userName, string accountKey)
        {
            var result = Query(EnumQueries.InsertAccountKeyByUsername, new { userName, accountKey });
            return result.Any();
        }

        public void Reset()
        {
            if (TableExists())
                DropTable();

            CreateTable();

            //Keys for SYSOP
            InsertAccountKeyByUsername("sysop", "DEMO");
            InsertAccountKeyByUsername("sysop", "NORMAL");
            InsertAccountKeyByUsername("sysop", "SUPER");
            InsertAccountKeyByUsername("sysop", "SYSOP");

            //Keys for GUEST
            InsertAccountKeyByUsername("guest", "DEMO");
            InsertAccountKeyByUsername("guest", "NORMAL");
        }
    }
}
