using MBBSEmu.Database.Session;

namespace MBBSEmu.Database.Repositories
{
    public class GeneralRepository : RepositoryBase, IGeneralRepository
    {
        public GeneralRepository(ISessionBuilder sessionBuilder) : base(sessionBuilder)
        {
            
        }
    }
}
