using System.ComponentModel;
using MBBSEmu.Database.Attributes;

namespace MBBSEmu.Database.Repositories.AccountKey.Queries
{
    public enum EnumQueries
    {
        [SqlQuery("CreateAccountKeysTable.sql")]
        [Description("Creates AccountKeys table")]
        CreateAccountKeysTable,

        [SqlQuery("AccountKeysTableExists.sql")]
        [Description("Checks to see if the table exists")]
        AccountKeysTableExists,

        [SqlQuery("InsertAccountKey.sql")]
        [Description("Inserts new Account Key")]
        InsertAccountKey,

        [SqlQuery("InsertAccountKeyByUsername.sql")]
        [Description("Inserts new Account Key by Username")]
        InsertAccountKeyByUsername,

        [SqlQuery("DropAccountKeysTable.sql")]
        [Description("Drops the Account Table")]
        DropAccountKeysTable,

        [SqlQuery("GetAccountKeysByAccountId.sql")]
        [Description("Gets Account Keys by the specified Account ID")]
        GetAccountKeysByAccountId,

        [SqlQuery("GetAccountKeysByUsername.sql")]
        [Description("Gets Account Keys by the specified Username")]
        GetAccountKeysByUsername,

        [SqlQuery("DeleteAccountKeyByUsernameAndAccountKey.sql")]
        [Description("Deletes the specified Account Key from the Specified Username")]
        DeleteAccountKeyByUsernameAndAccountKey
    }
}
