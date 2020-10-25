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

        public SessionBuilder(AppSettings appConfig)
        {
            var dbFile = appConfig.DatabaseFile;

            _connectionString = $"Data Source={dbFile};";
        }

        /// <summary>
        ///     Returns a new SqlConnection object in an Open state
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public DbConnection GetConnection(string connectionString)
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
