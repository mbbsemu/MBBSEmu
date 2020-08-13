namespace MBBSEmu.Tests
{
    public abstract class MBBSEmuTestBase
    {
        static MBBSEmuTestBase()
        {
            MBBSEmu.DependencyInjection.ServiceResolver.Create();
        }
    }
}