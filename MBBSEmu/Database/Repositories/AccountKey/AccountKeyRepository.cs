using MBBSEmu.Database.Repositories.AccountKey.Model;
using MBBSEmu.Database.Repositories.AccountKey.Queries;
using MBBSEmu.Database.Session;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.Database.Repositories.AccountKey
{
    public class AccountKeyRepository(ISessionBuilder sessionBuilder, IResourceManager resourceManager, AppSettingsManager appSettingsManager) 
        : RepositoryBase(sessionBuilder, resourceManager), IAccountKeyRepository
    {
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

        public void DeleteAccountKeyByUsernameAndAccountKey(string userName, string accountKey)
        {
            Query(EnumQueries.DeleteAccountKeyByUsernameAndAccountKey, new {userName, accountKey});
        }

        public void Reset()
        {
            if (TableExists())
                DropTable();

            CreateTable();

            //Keys for SYSOP
            InsertAccountKeyByUsername("sysop", "SUPER");
            InsertAccountKeyByUsername("sysop", "SYSOP");
            ApplyDefaultAccountKeys("sysop");


            //Keys for GUEST
            ApplyDefaultAccountKeys("guest");
            return;

            //Local Function to apply default account keys defined in AppSettingsManager
            void ApplyDefaultAccountKeys(string userName)
            {
                foreach (var accountKey in appSettingsManager.DefaultKeys)
                    InsertAccountKeyByUsername(userName, accountKey);
            }
        }
    }
}
