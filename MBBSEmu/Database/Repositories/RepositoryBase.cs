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
    public abstract class RepositoryBase : IRepositoryBase, IDisposable
    {
        private readonly IResourceManager _resourceManager;

        private readonly DbConnection _connection;

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        protected RepositoryBase(ISessionBuilder sessionBuilder, IResourceManager resourceManager)
        {
            _connection = sessionBuilder.GetConnection();
            _resourceManager = resourceManager;
        }

        public IEnumerable<T> Query<T>(object enumQuery, object parameters)
        {
            return _connection.Query<T>(_resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}"), parameters);
        }

        public IEnumerable<dynamic> Query(object enumQuery, object parameters)
        {
            var sql = _resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}");
            return _connection.Query(sql, parameters);
        }

        public IEnumerable<dynamic> Exec(string storedProcName, object parameters)
        {
            return _connection.Query(storedProcName, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
