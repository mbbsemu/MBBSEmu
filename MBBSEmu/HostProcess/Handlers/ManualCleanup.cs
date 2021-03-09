using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class ManualCleanupHandler : INotificationHandler<ManualCleanup>
    {
        private readonly IMbbsHost _host;

        public ManualCleanupHandler(IMbbsHost host)
        {
            _host = host;
        }

        public Task Handle(ManualCleanup cleanup, CancellationToken cancellationToken)
        {
            _host.ManualCleanup();

            return Task.CompletedTask;
        }
    }
}
