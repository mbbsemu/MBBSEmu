using System.Collections.Generic;

namespace MBBSEmu.Database.Repositories
{
    public interface IRepositoryBase
    {
        IEnumerable<dynamic> Query(object enumQuery, object parameters);
        IEnumerable<dynamic> Exec(string storedProcName, object parameters);
        IEnumerable<T> Query<T>(object enumQuery, object parameters);
    }
}
