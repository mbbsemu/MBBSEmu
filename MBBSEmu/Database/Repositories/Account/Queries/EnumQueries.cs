using MBBSEmu.Database.Attributes;
using System.ComponentModel;

namespace MBBSEmu.Database.Repositories.Account.Queries
{
    public enum EnumQueries
    {
        [SqlQuery("CreateAccountsTable.sql")]
        [Description("Creates Table for Account Records")]
        CreateAccountsTable,

        [SqlQuery("AccountsTableExists.sql")]
        [Description("Checks to see if the Account Table Exists")]
        AccountsTableExists,

        [SqlQuery("InsertAccount.sql")]
        [Description("Inserts new Account Record")]
        InsertAccount,

        [SqlQuery("DropAccountsTable.sql")]
        [Description("Drops the Account Table")]
        DropAccountsTable,

        [SqlQuery("GetAccountByUsername.sql")]
        [Description("Gets Account by the specified Username")]
        GetAccountByUsername,

        [SqlQuery("GetAccountByEmail.sql")]
        [Description("Gets Account by the specified Email")]
        GetAccountByEmail
    }
}
