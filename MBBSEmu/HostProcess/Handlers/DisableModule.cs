using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class DisableModuleHandler : INotificationHandler<DisableModule>
    {
        private readonly IMbbsHost _host;

        public DisableModuleHandler(IMbbsHost host)
        {
            _host = host;
        }

        public Task Handle(DisableModule moduleId, CancellationToken cancellationToken)
        {
            _host.DisableModule(moduleId.ModuleId);

            return Task.CompletedTask;
        }
    }
}
