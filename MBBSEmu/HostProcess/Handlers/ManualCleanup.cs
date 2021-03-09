using MBBSEmu.HostProcess.GlobalRoutines;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.HostProcess.Handlers
{
    public class ManualCleanupHandler : IRequestHandler<ManualCleanup, bool>
    {
        private readonly IMbbsHost _host;

        public ManualCleanupHandler(IMbbsHost host)
        {
            _host = host;
        }

        public Task<bool> Handle(ManualCleanup cleanup, CancellationToken cancellationToken)
        {
            _host.ManualCleanup();

            return Task.FromResult(true);
        }
    }
}
