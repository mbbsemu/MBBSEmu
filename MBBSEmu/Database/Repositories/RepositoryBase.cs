using Dapper;
using MBBSEmu.Database.Attributes;
using MBBSEmu.Database.Session;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.Data;

namespace MBBSEmu.Database.Repositories
{
    /// <summary>
    ///     Base Class for Repositories
    ///
    ///     Holds common Dapper Functionality for executing Queries
    /// </summary>
    public abstract class RepositoryBase : IRepositoryBase
    {
        private readonly IResourceManager _resourceManager;
        private readonly ISessionBuilder _sessionBuilder;

        protected RepositoryBase(ISessionBuilder sessionBuilder, IResourceManager resourceManager)
        {
            _sessionBuilder = sessionBuilder;
            _resourceManager = resourceManager;
        }

        public IEnumerable<T> Query<T>(object enumQuery, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            return connection.Query<T>(_resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}"), parameters);
            
        }

        public IEnumerable<dynamic> Query(object enumQuery, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            var sql = _resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}");
            return connection.Query(sql, parameters);
        }

        public IEnumerable<dynamic> Exec(string storedProcName, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            return connection.Query(storedProcName, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}