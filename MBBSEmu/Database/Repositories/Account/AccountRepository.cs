﻿using MBBSEmu.Database.Repositories.Account.Model;
using MBBSEmu.Database.Repositories.Account.Queries;
using MBBSEmu.Database.Session;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MBBSEmu.Database.Repositories.Account
{
    /// <summary>
    ///     Repository Pattern for the MBBSEmu Account Database
    /// </summary>
    public class AccountRepository : RepositoryBase, IAccountRepository
    {
        public AccountRepository(ISessionBuilder sessionBuilder, IResourceManager resourceManager) : base(sessionBuilder, resourceManager)
        {
        }

        public bool CreateTable()
        {
            var result = Query(EnumQueries.CreateAccountsTable, null);
            return true;
        }

        public bool TableExists()
        {
            var result = Query(EnumQueries.AccountsTableExists, null);
            return result.Any();
        }

        public bool DropTable()
        {
            var result = Query(EnumQueries.DropAccountsTable, null);
            return result.Any();
        }

        public int InsertAccount(string userName, string plaintextPassword, string email)
        {
            var passwordSaltBytes = GenerateSalt();
            var passwordHashBytes = CreateSHA512(Encoding.Default.GetBytes(plaintextPassword), passwordSaltBytes);
            var passwordSalt = System.Convert.ToBase64String(passwordSaltBytes);
            var passwordHash = System.Convert.ToBase64String(passwordHashBytes);

            var result = Query<int>(EnumQueries.InsertAccount, new { userName, passwordHash, passwordSalt, email });
            return result.First();
        }

        public AccountModel GetAccountByUsername(string userName)
        {
            return Query<AccountModel>(EnumQueries.GetAccountByUsername, new { userName }).FirstOrDefault();
        }

        public AccountModel GetAccountByUsernameAndPassword(string userName, string password)
        {
            var account = GetAccountByUsername(userName);
            if (account == null)
                return null;

            var passwordHashBytes = CreateSHA512(Encoding.Default.GetBytes(password),
                System.Convert.FromBase64String(account.passwordSalt));
            var passwordHash = System.Convert.ToBase64String(passwordHashBytes);

            return account.passwordHash == passwordHash ? account : null;
        }

        public AccountModel GetAccountByEmail(string email)
        {
            return Query<AccountModel>(EnumQueries.GetAccountByEmail, new { email }).FirstOrDefault();
        }

        public IEnumerable<AccountModel> GetAccounts() => Query<AccountModel>(EnumQueries.GetAccounts, null);

        public AccountModel GetAccountById(int accountId) =>
            Query<AccountModel>(EnumQueries.GetAccountById, new { accountId }).FirstOrDefault();

        public void DeleteAccountById(int accountId)
        {
            Query(EnumQueries.DeleteAccountById, new {accountId});
        }

        public void UpdateAccountById(int accountId, string userName, string plaintextPassword, string email)
        {
            var passwordSaltBytes = GenerateSalt();
            var passwordHashBytes = CreateSHA512(Encoding.Default.GetBytes(plaintextPassword), passwordSaltBytes);
            var passwordSalt = System.Convert.ToBase64String(passwordSaltBytes);
            var passwordHash = System.Convert.ToBase64String(passwordHashBytes);

            Query(EnumQueries.UpdateAccountById, new { accountId, userName, passwordHash, passwordSalt, email });
        }

        public void Reset(string sysopPassword)
        {
            if (TableExists())
                DropTable();

            CreateTable();

            InsertAccount("sysop", sysopPassword, "sysop@mbbsemu.com");
            InsertAccount("guest", "guest", "guest@mbbsemu.com");
        }

        /// <summary>
        ///     Generates a cryptographically strong random sequence of bytes
        ///
        ///     Ideally used for a cryptographic salt
        /// </summary>
        /// <param name="saltLength"></param>
        /// <returns></returns>
        private byte[] GenerateSalt(int saltLength = 32)
        {
            using var rngProvider = RandomNumberGenerator.Create();
            var randomBytes = new byte[saltLength];
            rngProvider.GetBytes(randomBytes);
            return randomBytes;
        }

        /// <summary>
        ///     Hashes an input byte array with the specified salt
        /// </summary>
        /// <param name="valueToHash"></param>
        /// <param name="salt"></param>
        /// <returns></returns>
        public byte[] CreateSHA512(byte[] valueToHash, byte[] salt)
        {
            var passwordWithSaltBytes = new List<byte>(valueToHash.Length + salt.Length);
            passwordWithSaltBytes.AddRange(valueToHash);
            passwordWithSaltBytes.AddRange(salt);
            return SHA512.Create().ComputeHash(passwordWithSaltBytes.ToArray());
        }
    }
}
