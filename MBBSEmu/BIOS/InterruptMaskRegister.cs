using MBBSEmu.CPU;
using System;

namespace MBBSEmu.BIOS
{
    /// <summary>
    ///     Compatibility register for modules that save and restore the master PIC mask.
    ///     This stores OCW1 only; it does not generate IRQs or implement PIC commands.
    /// </summary>
    public sealed class InterruptMaskRegister : IIOPort
    {
        public const byte Port = 0x21;

        // Module execution has no hardware IRQ sources to enable.
        private byte _mask = byte.MaxValue;

        public byte In(byte channel)
        {
            if (channel != Port)
                throw new ArgumentOutOfRangeException(nameof(channel));

            return _mask;
        }

        public void Out(byte channel, byte value)
        {
            if (channel != Port)
                throw new ArgumentOutOfRangeException(nameof(channel));

            _mask = value;
        }
    }
}
