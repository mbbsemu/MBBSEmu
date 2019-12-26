using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace MBBSEmu.Database.Session
{
    public class SessionBuilder : ISessionBuilder
    {
        private readonly string _connectionString;

        public DbConnection GetConnection() => GetConnection(_connectionString);

        public SessionBuilder(IConfigurationRoot appConfig)
        {
            var dbFile = appConfig["Database.File"];

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
