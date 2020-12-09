namespace MBBSEmu.DOS.Interrupts
{
    public interface IInterruptHandler
    {
        byte Vector { get; }
        void Handle();
    }
}
