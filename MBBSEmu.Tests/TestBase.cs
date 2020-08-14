using MBBSEmu;

namespace MBBSEmu.Tests
{
    public abstract class TestBase
    {
        static TestBase()
        {
            DependencyInjection.ServiceResolver.Create(Program.DefaultEmuSettingsFilename);
        }
    }
}
