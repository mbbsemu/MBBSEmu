using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace MBBSEmu.Database.Session
{
    public class SessionBuilder : ISessionBuilder
    {
        private readonly string _connectionString;

        public DbConnection GetConnection() => GetConnection(_connectionString);

        public SessionBuilder(string connectionString)
        {
            _connectionString = connectionString;
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
