using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace MBBSEmu.Database.Session
{
    /// <summary>
    ///     Handles creating new connections to the Database
    /// </summary>
    public class SessionBuilder : ISessionBuilder
    {
        private readonly string _connectionString;

        /// <summary>
        ///     Returns a new SqlConnection object in an Open state with the default Connection String
        /// </summary>
        /// <returns></returns>
        public DbConnection GetConnection() => GetConnection(_connectionString);

        public SessionBuilder(AppSettingsManager appConfig)
        {
            var dbFile = appConfig.DatabaseFile;

            _connectionString = $"Data Source={dbFile};";
        }

        private SessionBuilder(string databaseName)
        {
            _connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        }

        public static SessionBuilder ForTest(string databaseName)
        {
            return new SessionBuilder(databaseName);
        }

        /// <summary>
        ///     Returns a new SqlConnection object in an Open state
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        private DbConnection GetConnection(string connectionString)
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
