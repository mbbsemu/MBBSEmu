using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Logging;
using MBBSEmu.Server;
using System;
using System.Threading;

namespace MBBSEmu.BIOS
{
    //http://staff.ustc.edu.cn/~xyfeng/research/cos/resources/BIOS/Resources/assembly/inttable.html
    public class ProgrammableIntervalTimer : IIOPort, IStoppable, IDisposable
    {
        public const double FREQUENCY = 1_193_181.666666666666666666;

        private enum AccessMode
        {
            LATCH_VALUE = 0,
            LOBYTE_ONLY = 1,
            HIBYTE_ONLY = 2,
            LO_THEN_HIBYTE_VALUE = 3,
        }

        private enum OperatingMode
        {
            MODE_0_INTERRUPT_ON_TERMINAL_COUNT = 0,
            MODE_1_HARDWARE_RETRIGGERABLE_ONE_SHOT = 1,
            MODE_2_RATE_GENERATOR = 2,
            MODE_3_SQUARE_WAVE_GENERATOR = 3,
            MODE_4_SOFTWARE_TRIGGERED_STROBE = 4,
            MODE_5_HARDWARE_TRIGGERED_STROBE = 5,
        }

        private struct ChannelConfig
        {
            public AccessMode AccessMode;
            public OperatingMode OperatingMode;
            public ushort latchedValue;
            public int counter;
        }

        private readonly ChannelConfig[] _channelConfig = new ChannelConfig[3];

        private readonly IMessageLogger _logger;
        private readonly IClock _clock;
        private readonly ICpuCore _cpu;
        private Timer _timer;

        public ProgrammableIntervalTimer(IMessageLogger logger, IClock clock, ICpuCore cpu)
        {
            _logger = logger;
            _clock = clock;
            _cpu = cpu;
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private ushort TickToUShort()
        {
            var elapsed = _clock.CurrentTick;
            var fractional = elapsed - Math.Truncate(elapsed);
            var current = FREQUENCY * (1.0 - fractional);
            return (ushort)current;
        }

        private byte ReadLowHighFrom(int channel, ushort value)
        {
            var counter = _channelConfig[channel].counter++;
            if ((counter & 1) == 0)
                return (byte)value;

            return (byte)(value >> 8);
        }

        public byte In(byte channel)
        {
            channel -= 0x40;

            switch (_channelConfig[channel].AccessMode)
            {
                case AccessMode.LOBYTE_ONLY:
                    _channelConfig[channel].counter = 0;
                    return (byte)TickToUShort();
                case AccessMode.HIBYTE_ONLY:
                    _channelConfig[channel].counter = 0;
                    return (byte)(TickToUShort() >> 8);
                case AccessMode.LATCH_VALUE:
                    return ReadLowHighFrom(channel, _channelConfig[channel].latchedValue);
                case AccessMode.LO_THEN_HIBYTE_VALUE:
                    return ReadLowHighFrom(channel, TickToUShort());
            }
            return 0;
        }

        public void Out(byte channel, byte b)
        {
            /*
            Bits         Usage
6 and 7      Select channel :
                0 0 = Channel 0
                0 1 = Channel 1
                1 0 = Channel 2
                1 1 = Read-back command (8254 only)
4 and 5      Access mode :
                0 0 = Latch count value command
                0 1 = Access mode: lobyte only
                1 0 = Access mode: hibyte only
                1 1 = Access mode: lobyte/hibyte
1 to 3       Operating mode :
                0 0 0 = Mode 0 (interrupt on terminal count)
                0 0 1 = Mode 1 (hardware re-triggerable one-shot)
                0 1 0 = Mode 2 (rate generator)
                0 1 1 = Mode 3 (square wave generator)
                1 0 0 = Mode 4 (software triggered strobe)
                1 0 1 = Mode 5 (hardware triggered strobe)
                1 1 0 = Mode 2 (rate generator, same as 010b)
                1 1 1 = Mode 3 (square wave generator, same as 011b)
0            BCD/Binary mode: 0 = 16-bit binary, 1 = four-digit BCD
*/
            if (channel != 0x43)
                throw new ArgumentException($"Can't write to channel {channel:X2}h");

            if ((b & 1) == 1)
                throw new ArgumentException("BCD PIT not supported");

            var pitChannel = b >> 6;
            var accessMode = (AccessMode)((b >> 4) & 0x3);
            var operatingMode = ((b >> 1) & 0x7);
            if (operatingMode > 5)
                operatingMode &= 0x3;

            _channelConfig[pitChannel].AccessMode = accessMode;
            _channelConfig[pitChannel].OperatingMode = (OperatingMode)operatingMode;

            if (accessMode == AccessMode.LATCH_VALUE)
                _channelConfig[pitChannel].latchedValue = TickToUShort();

            if (pitChannel == 0 && operatingMode == (int)OperatingMode.MODE_0_INTERRUPT_ON_TERMINAL_COUNT)
            {
                var interval = TimeSpan.FromMilliseconds(1000 / 18);

                _timer ??= new Timer(_ => _cpu.Interrupt(8), null, TimeSpan.Zero, interval);
            }
            else if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            //_logger?.Debug($"PIT channel {pitChannel} is now {accessMode}:{(OperatingMode)operatingMode}");
        }
    }
}
