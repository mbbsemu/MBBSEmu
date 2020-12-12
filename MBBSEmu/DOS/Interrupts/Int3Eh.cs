namespace MBBSEmu.DOS.Interrupts
{
    /// <summary>
    ///     FLOATING POINT EMULATION -- Borland "Shortcut" call
    ///
    ///     TODO: This Interrupt is Not Implemented/Ignored within MBBSEmu, for now
    /// </summary>
    public class Int3Eh : IInterruptHandler
    {
        public byte Vector => 0x3E;
        public void Handle()
        {
            return;
        }
    }
}
