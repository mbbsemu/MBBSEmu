namespace MBBSEmu.DOS.Interrupts
{
    public interface IInterruptHandler
    {
        ushort Vector { get; }
        void Handle();
    }
}
