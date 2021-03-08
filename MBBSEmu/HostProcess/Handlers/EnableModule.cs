using System.Threading;
using System.Threading.Tasks;
using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;

namespace MBBSEmu.HostProcess.Handlers
{
    public class EnableModuleHandler : IRequestHandler<EnableModule, bool>
    {
        public Task<bool> Handle(EnableModule moduleId, CancellationToken cancellationToken)
        {
            var _moduleId = moduleId;
            
            //TODO I want to call MBBSHOST.EnableModule(moduleId) here

            return Task.FromResult(true);

        }
    }
}