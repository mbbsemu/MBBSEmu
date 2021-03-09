using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class DisableModuleHandler : IRequestHandler<DisableModule, bool>
    {
        private IMbbsHost _host;

        public void DisableModuleService(IMbbsHost host) => _host = host;

        public Task<bool> Handle(DisableModule moduleId, CancellationToken cancellationToken)
        {
            var _moduleId = moduleId;

            _host.DisableModule(_moduleId.ModuleId);

            return Task.FromResult(true);

        }
    }
}
