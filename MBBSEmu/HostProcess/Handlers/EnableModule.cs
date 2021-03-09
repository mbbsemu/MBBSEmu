using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class EnableModuleHandler : INotificationHandler<EnableModule>
    {
        private readonly IMbbsHost _host;

        public EnableModuleHandler(IMbbsHost host)
        {
            _host = host;
        }

        public Task Handle(EnableModule moduleId, CancellationToken cancellationToken)
        {
            _host.EnableModule(moduleId.ModuleId);

            return Task.CompletedTask;
        }
    }
}
