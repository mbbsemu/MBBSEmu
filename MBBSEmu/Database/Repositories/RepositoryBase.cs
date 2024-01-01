using Dapper;
using MBBSEmu.Database.Attributes;
using MBBSEmu.Database.Session;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System;

namespace MBBSEmu.Database.Repositories
{
    /// <summary>
    ///     Base Class for Repositories
    ///
    ///     Holds common Dapper Functionality for executing Queries
    /// </summary>
    public abstract class RepositoryBase(ISessionBuilder sessionBuilder, IResourceManager resourceManager) : IRepositoryBase, IDisposable
    {
        private readonly DbConnection _connection = sessionBuilder.GetConnection();

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        public IEnumerable<T> Query<T>(object enumQuery, object parameters)
        {
            return _connection.Query<T>(resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}"), parameters);
        }

        public IEnumerable<dynamic> Query(object enumQuery, object parameters)
        {
            var sql = resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}");
            return _connection.Query(sql, parameters);
        }

        public IEnumerable<dynamic> Exec(string storedProcName, object parameters)
        {
            return _connection.Query(storedProcName, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
