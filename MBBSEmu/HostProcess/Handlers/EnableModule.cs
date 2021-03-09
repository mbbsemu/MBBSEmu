using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class EnableModuleHandler : IRequestHandler<EnableModule, bool>
    {
        private readonly IMbbsHost _host;

        public EnableModuleHandler(IMbbsHost host)
        {
            _host = host;
        }

        public Task<bool> Handle(EnableModule moduleId, CancellationToken cancellationToken)
        {
            var _moduleId = moduleId;
            
            _host.EnableModule(_moduleId.ModuleId);

            return Task.FromResult(true);
        }
    }
}
