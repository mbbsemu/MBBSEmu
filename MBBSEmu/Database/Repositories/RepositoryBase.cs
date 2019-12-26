using Dapper;
using MBBSEmu.Database.Attributes;
using MBBSEmu.Database.Session;
using MBBSEmu.Resources;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace MBBSEmu.Database.Repositories
{
    /// <summary>
    ///     Base Class for Repositories
    /// </summary>
    public abstract class RepositoryBase : IRepositoryBase
    {
        private readonly IResourceManager _resourceManager = new ResourceManager(Assembly.GetExecutingAssembly());
        private readonly ISessionBuilder _sessionBuilder;

        protected RepositoryBase(ISessionBuilder sessionBuilder)
        {
            _sessionBuilder = sessionBuilder;
        }

        public IEnumerable<T> Query<T>(object enumQuery, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            return connection.Query<T>(_resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}"), parameters);
        }

        /// <summary>
        ///     Synchronous implementation of QueryAsync
        /// </summary>
        /// <param name="enumQuery"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public IEnumerable<dynamic> Query(object enumQuery, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            return connection.Query(_resourceManager.GetString($"{SqlQueryAttribute.Get(enumQuery)}"), parameters);
        }

        public IEnumerable<dynamic> Exec(string storedProcName, object parameters)
        {
            using var connection = _sessionBuilder.GetConnection();
            return connection.Query(storedProcName, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}