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
            
            return Task.FromResult(true);

        }
    }
}