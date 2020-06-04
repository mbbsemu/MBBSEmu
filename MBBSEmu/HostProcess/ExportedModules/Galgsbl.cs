using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions &amp; properties that are part of the Galacticomm
    ///     Global Software Breakout Library (GALGSBL.H). 
    /// </summary>
    public class Galgsbl : ExportedModuleBase, IExportedModule
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFE;

        private ushort MonitoredChannel { get; set; }
        private ushort MonitoredChannel2 { get; set; }

        private readonly DateTime _startDate;

        public Galgsbl(MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(module, channelDictionary)
        {
            _startDate = DateTime.Now;
            var bturnoPointer = Module.Memory.AllocateVariable("BTURNO", 9);
            Module.Memory.SetArray(bturnoPointer, Encoding.ASCII.GetBytes($"{_configuration["GSBL.Activation"]}\0"));
            Module.Memory.AllocateVariable("TICKER", 0x02); //ushort increments once per second

            MonitoredChannel2 = 0xFFFF;
            MonitoredChannel = 0xFFFF;

            new Thread(Timer1Hz).Start();
        }

        public void UpdateSession(ushort channel)
        {

        }

        public void SetRegisters(CpuRegisters registers)
        {
            Registers = registers;
        }

        /// <summary>
        ///     Internal timer for operations that need to happen every 1 seconds
        /// </summary>
        private void Timer1Hz()
        {
            while (true)
            {
                //Update TICKER
                var tickerPointer = Module.Memory.GetVariablePointer("TICKER");
                var seconds = (ushort) ((DateTime.Now - _startDate).TotalSeconds % 0xFFFF);
                Module.Memory.SetWord(tickerPointer, seconds);
                Thread.Sleep(999);
            }
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            switch (ordinal)
            {
                case 72:
                    return bturno();
                case 65:
                    return ticker;
            }

            if (offsetsOnly)
            {
                var methodPointer = new IntPtr16(0xFFFE, ordinal);
#if DEBUG
                //_logger.Info($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.ToSpan();
            }

            switch (ordinal)
            {
                
                case 36:
                    btuoba();
                    break;
                case 49:
                    btutrg();
                    break;
                case 21:
                    btuinj();
                    break;
                case 60:
                    btuxnf();
                    break;
                case 39:
                    btupbc();
                    break;
                case 87:
                    btuica();
                    break;
                case 6:
                    btucli();
                    break;
                case 4:
                    btuchi();
                    break;
                case 63:
                    chious();
                    break;
                case 83:
                    btueba();
                    break;
                case 19:
                    btuibw();
                    break;
                case 59:
                    btuxmt();
                    break;
                case 7:
                    btuclo();
                    break;
                case 30:
                    btumil();
                    break;
                case 3:
                    btuche();
                    break;
                case 5:
                    btuclc();
                    break;
                case 8:
                    btucls();
                    break;
                case 52:
                    btutru();
                    break;
                case 37:
                    btuoes();
                    break;
                case 11:
                    btuech();
                    break;
                case 53:
                    btutsw();
                    break;
                case 58:
                    btuxmn();
                    break;
                case 34:
                    btumon2();
                    break;
                case 32:
                    btumks2();
                    break;
                case 48:
                    btusts();
                    break;
                case 29:
                    btumds2();
                    break;
                case 41:
                    bturst();
                    break;
                case 40:
                    btupmt();
                    break;
                case 9:
                    btucmd();
                    break;
                case 56:
                    btuxct();
                    break;
                case 64:
                    chiout();
                    break;
                case 61:
                    chiinj();
                    break;
                case 15:
                    btuhcr();
                    break;
                case 44:
                    btuscr();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in GALGSBL: {ordinal}");
            }

            return null;
        }

        public void SetState(ushort channelNumber)
        {
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("USERNUM"), channelNumber);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> bturno() => Module.Memory.GetVariablePointer("BTURNO").ToSpan();

        /// <summary>
        ///     Report the amount of space (number of bytes) available in the output buffer
        ///     Since we're not using a dialup terminal or any of that, we'll just set it to ushort.MaxValue
        ///
        ///     Signature: int btuoba(int chan)
        ///     Result: AX == bytes available
        /// </summary>
        /// <returns></returns>
        public void btuoba()
        {
            Registers.AX = (ushort)short.MaxValue;
        }

        /// <summary>
        ///     Set the input byte trigger quantity (used in conjunction with btuict())
        ///
        ///     Signature: int btutrg(int chan,int nbyt)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public void btutrg()
        {
            var channel = GetParameter(0);
            var numBytes = GetParameter(1);

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

            if (numBytes == 0)
            {
                //Default ASCII mode -- we don't need to do anything
                ChannelDictionary[channel].TransparentMode = false;
                Registers.AX = 0;
                return;
            }

            if (numBytes >= 1)
            {
                ChannelDictionary[channel].TransparentMode = true;
                Registers.AX = 0;
                return;
            }

            throw new ArgumentOutOfRangeException($"Invalid value for numBytes: {numBytes}");
        }

        /// <summary>
        ///     Inject a status code into a channel
        /// 
        ///     Signature: int btuinj(int chan,int status)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public void btuinj()
        {
            var channel = GetParameter(0);
            var status = GetParameter(1);

            //Status Change
            //Set the Memory Value
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), status);

            //Notify the Session that a Status Change has occured
            ChannelDictionary[channel].StatusChange = true;

            Registers.AX = 0;
        }

        /// <summary>
        ///     Set XON/XOFF characters, select page mode
        ///
        ///     Signature: int btuxnf(int chan,int xon,int xoff,...)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public void btuxnf()
        {
            //Ignore this, we won't deal with XON/XOFF
            Registers.AX = 0;
        }

        /// <summary>
        ///     Set screen-pause character
        ///     Pauses the screen when in the output stream
        ///
        ///     Puts the screen in screen-pause mode
        ///     Signature: int err=btupbc(int chan, char pausch)
        ///     Result: AX == 0 = OK
        /// </summary>
        /// <returns></returns>
        public void btupbc()
        {
            //TODO -- Handle this?
            Registers.AX = 0;
        }

        /// <summary>
        ///     Input from a channel - reading in whatever bytes are available, up to a limit
        ///
        ///     Signature: int btuica(int chan,char *rdbptr,int max)
        ///     Result: AX == Number of input characters retrieved
        /// </summary>
        /// <returns></returns>
        public void btuica()
        {
            var channelNumber = GetParameter(0);
            var destinationOffset = GetParameter(1);
            var destinationSegment = GetParameter(2);
            var max = GetParameter(3);

            //Nothing to Input?
            if (ChannelDictionary[channelNumber].InputBuffer.Length == 0)
            {
                Registers.AX = 0;
                return;
            }

            int bytesToRead;
            if (max > ChannelDictionary[channelNumber].InputBuffer.Length)
                bytesToRead = (int) ChannelDictionary[channelNumber].InputBuffer.Length;
            else
                bytesToRead = max;

            var bytesRead = new byte[bytesToRead];
            ChannelDictionary[channelNumber].InputBuffer.Position = 0;
            ChannelDictionary[channelNumber].InputBuffer.Read(bytesRead, 0, bytesToRead);

            Module.Memory.SetArray(destinationSegment, destinationOffset, bytesRead);
            Registers.AX = (ushort) bytesToRead;
        }

        /// <summary>
        ///     Clears the input buffer
        ///
        ///     Since our input buffer is a queue, we'll just clear it
        /// 
        ///     Signature: int btucli(int chan)
        ///     Result: 
        /// </summary>
        /// <returns></returns>
        public void btucli()
        {
            var channelNumber = GetParameter(0);

            if (!ChannelDictionary.ContainsKey(channelNumber))
            {
                Registers.AX = 0;
                return;
            }

            ChannelDictionary[channelNumber].InputBuffer.SetLength(0);

            Registers.AX = 0;
        }

        /// <summary>
        ///     Sets Input Character Interceptor
        ///
        ///     Signature: int err=btuchi(int chan, char (*rouadr)())
        /// </summary>
        /// <returns></returns>
        public void btuchi()
        {

            var channel = GetParameter(0);
            var routinePointer = GetParameterPointer(1);

            //Unset on the specified channel
            if (routinePointer.Segment == 0 && routinePointer.Offset == 0)
            {

                ChannelDictionary[channel].CharacterInterceptor = null;
                Registers.AX = 0;

#if DEBUG
                _logger.Info($"Unassigned Character Interceptor Routine on Channel {channel}");
#endif
                return;
            }

            ChannelDictionary[channel].CharacterInterceptor = new IntPtr16(routinePointer.ToSpan());

#if DEBUG
            _logger.Info($"Assigned Character Interceptor Routine {ChannelDictionary[channel].CharacterInterceptor} to Channel {channel}");
#endif

            Registers.AX = 0;
        }

        /// <summary>
        ///     Echo buffer space available for bytes
        ///
        ///     Signature: int btueba(int chan)
        ///     Returns: 0 == buffer is full
        ///              1-254 == Buffer is between full and empty
        ///              255 == Buffer is full
        /// </summary>
        /// <returns></returns>
        public void btueba()
        {
            var channel = GetParameter(0);

            //Always return that the echo buffer is empty, as 
            //we send data immediately to the client when it's 
            //written to the echo buffer (see chious())
            Registers.AX = 255;
        }

        /// <summary>
        ///     The Input Buffer Size
        /// </summary>
        public void btuibw()
        {
            var channelNumber = GetParameter(0);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ushort.MaxValue - 1;
                return;
            }

            Registers.AX = (ushort) channel.InputBuffer.Length;
        }

        /// <summary>
        ///     String Output (via Echo Buffer)
        /// </summary>
        /// <returns></returns>
        public void chious()
        {
            var channel = GetParameter(0);
            var stringOffset = GetParameter(1);
            var stringSegment = GetParameter(2);

            ChannelDictionary[channel].SendToClient(Module.Memory.GetString(stringSegment, stringOffset).ToArray());
        }

        /// <summary>
        ///     Transmit to channel (ASCIIZ string)
        ///
        ///     Signature: int btuxmt(int chan,char *datstg)
        /// </summary>
        public void btuxmt()
        {
            var channel = GetParameter(0);
            var stringPointer = GetParameterPointer(1);


            var stringToSend = Module.Memory.GetString(stringPointer);

            var formattedString = ProcessIfANSI(stringToSend);

            ChannelDictionary[channel].SendToClient(formattedString.ToArray());

            Registers.AX = 0;
        }

        /// <summary>
        ///     Clear data output buffer
        ///
        ///     Signature: int btuclo(int chan)
        ///     Returns: AX == 0, all is well
        /// </summary>
        private void btuclo()
        {
            Registers.AX = 0;
        }


        /// <summary>
        ///     Sets maximum input line length, sets word wrap on/off
        ///
        ///     Basically limits the maximum number of bytes a user can input
        ///     any bytes input past this limit should be ignored, but will generate
        ///     a status of 251
        ///
        ///     Signature: int err=btumil(int chan, int maxinl)
        /// </summary>
        private void btumil()
        {
            Registers.AX = 0;
        }

        /// <summary>
        ///     Enables calling of btuchi() when echo buffer becomes empty
        ///
        ///     Signature: int err=btuche(int chan, int onoff)
        /// </summary>
        private void btuche()
        {
            //TODO -- Ignoring this for now, need to better understand the effect
            Registers.AX = 0;
        }

        /// <summary>
        ///     Clears Command Input Buffer
        ///
        ///     Signature: int btuclc(int chan)
        /// </summary>
        private void btuclc()
        {
            var channel = GetParameter(0);
            ChannelDictionary[channel].InputCommand = new byte[] {0x0};

            Registers.AX = 0;
        }

        /// <summary>
        ///     Clear status input buffer
        ///
        ///     Signature: int btucls(int chan)
        /// </summary>
        private void btucls()
        {
            var channel = GetParameter(0);

            //TODO -- not sure the functionality here, need to research

            Registers.AX = 0;
        }

        /// <summary>
        ///     Sets output-abort character
        ///
        ///     Signature: int btutru(int chan,char trunch)
        /// </summary>
        private void btutru()
        {
            //TODO -- not sure the functionality here, need to research
            Registers.AX = 0;
        }

        /// <summary>
        ///     Enable/Disable Output-Empty status codes
        ///
        ///     Signature: int btuoes(int chan,int onoff);
        /// </summary>
        private void btuoes()
        {
            var channel = GetParameter(0);
            var onoff = GetParameter(1);

            //Notify the Session that a Status Change has occured
            if (onoff == 1)
            {
                ChannelDictionary[channel].OutputEmptyStatus = true;
                ChannelDictionary[channel].StatusChange = true;
                Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), 5); 
            }
            else
            {
                ChannelDictionary[channel].OutputEmptyStatus = false;
                ChannelDictionary[channel].StatusChange = true;
            }

            Registers.AX = 0;
        }

        /// <summary>
        ///     Set Echo on/off
        ///
        ///     Signature: int btuech(int chan, int mode)
        /// </summary>
        private void btuech()
        {
            var channel = GetParameter(0);
            var mode = GetParameter(1);

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

#if DEBUG
            _logger.Info($"Setting ECHO to: {mode == 0}");
#endif
            ChannelDictionary[channel].TransparentMode = mode == 0;
            Registers.AX = 0;
        }

        /// <summary>
        ///     Sets Screen Width (ignored)
        /// </summary>
        private void btutsw()
        {
            var channel = GetParameter(0);
            var width = GetParameter(1);

#if DEBUG
            _logger.Warn($"Set Screen Width for Channel {channel} to {width}");
#endif
            Registers.AX = 0;
        }

        /// <summary>
        ///     Sends a message directly to another user
        /// </summary>
        private void btuxmn()
        {
            var channel = GetParameter(0);
            var messagePointer = GetParameterPointer(1);

            var messageToSend = Module.Memory.GetString(messagePointer);
            ChannelDictionary[channel].SendToClient(messageToSend.ToArray());
        }

        private void btumon2()
        {
            var channel = GetParameter(0);

            //Disable Monitoring
            if (channel == 0xFFFF)
            {
#if DEBUG
                _logger.Info($"Disabling Monitoring on all Channels");
#endif
                foreach (var c in ChannelDictionary.Values)
                {
                    c.Monitored = false;
                }

                MonitoredChannel2 = 0xFFFF;
                Registers.AX = 0;
            }

            if (!ChannelDictionary.TryGetValue(channel, out var userChannel))
            {
                Registers.AX = 0;
                return;
            }

            userChannel.Monitored2 = true;
            MonitoredChannel2 = channel;
            Registers.AX = 0;
#if DEBUG
            _logger.Info($"Enabled Monitoring on Channel {channel}");
#endif
        }

        private void btumks2()
        {
            var character = GetParameter(0);

            if (MonitoredChannel2 == 0xFFFF)
                return;

            ChannelDictionary[MonitoredChannel2].LastCharacterReceived = (byte) character;
            ChannelDictionary[MonitoredChannel2].InputBuffer.WriteByte((byte) character);
            ChannelDictionary[MonitoredChannel2].DataToProcess = true;
        }

        private ReadOnlySpan<byte> ticker => Module.Memory.GetVariablePointer("TICKER").ToSpan();

        /// <summary>
        ///     Status of a Channel
        ///
        ///     Signature: int btusts(int chan)
        /// </summary>
        private void btusts()
        {
            var channel = GetParameter(0);

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

            if (ChannelDictionary[channel].DataToProcess && ChannelDictionary[channel].Status == 3)
            {
                Registers.AX = 3;
            }
            else
            {
                Registers.AX = 0;
            }
        }

        /// <summary>
        ///     Gets Last Character Echo'd to a Channel
        /// </summary>
        private void btumds2()
        {
            if (MonitoredChannel2 == 0xFFFF)
            {
                Registers.AX = 0;
                return;
            }

            Registers.AX = 0;
        }


        /// <summary>
        ///     Resets A Channel
        /// </summary>
        private void bturst()
        {
            Registers.AX = 0;
        }

        /// <summary>
        ///     Set prompt character
        ///
        ///     Signature: int btupmt(int chan, char pmchar)
        /// </summary>
        private void btupmt()
        {
            var channel = GetParameter(0);
            var character = GetParameter(1);

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

            ChannelDictionary[channel].PromptCharacter = (byte) character;
            Registers.AX = 0;
        }


        /// <summary>
        ///     This routine controls functions of the UART and (if used) the modem on a channel
        /// 
        ///     Signature: int btucmd(int chan,char *cmdstg);
        /// </summary>
        private void btucmd()
        {
            var channel = GetParameter(0);
            var command = GetParameterPointer(1);

            var commandString = Encoding.ASCII.GetString(Module.Memory.GetString(command, true));

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

            switch (commandString)
            {
                case "[":
                    _logger.Info($"Enable ANSI on channel {channel} (Ignored)");
                    return;
                case "]":
                    _logger.Info($"Disable ANSI on channel {channel} (Ignored)");
                    return;
            }
        }

        /// <summary>
        ///
        ///
        ///     Signature: int btuxct(int chan,int nbyt,char *datstg)
        /// </summary>
        private void btuxct()
        {
            var channel = GetParameter(0);
            var bytesToSend = GetParameter(1);
            var bytesPointer = GetParameterPointer(2);

            if (!ChannelDictionary.ContainsKey(channel))
            {
                Registers.AX = 0;
                return;
            }

            var dataToSend = Module.Memory.GetArray(bytesPointer, bytesToSend).ToArray();

            ChannelDictionary[channel].SendToClient(dataToSend);
        }

        /// <summary>
        ///     Character Output (via Echo Buffer)
        ///
        ///     Signature: void chiout(int chan,char c);
        /// </summary>
        private void chiout()
        {
            var channel = GetParameter(0);
            var byteToSend = GetParameter(1);

            ChannelDictionary[channel].SendToClient(new byte[] {(byte) byteToSend});
        }

        /// <summary>
        ///     Status Inject Utility
        ///
        ///     Signature: void chiinj(int chan,int status)
        /// </summary>
        private void chiinj()
        {
            var channel = GetParameter(0);
            var status = GetParameter(1);

            ChannelDictionary[channel].Status = status;
            ChannelDictionary[channel].StatusChange = true;

            Module.Memory.SetVariable("STATUS", status);

#if DEBUG
            _logger.Info($"Injecting Status {status} on Channel {channel}");
#endif
        }

        /// <summary>
        ///     Sets hard-CR Character
        ///
        ///     Signature: int btuhcr(int chan,char hardcr);
        /// </summary>
        private void btuhcr()
        {
            var channel = GetParameter(0);
            var character = GetParameter(1);
            _logger.Info($"Set hard-CR character {character:X2} on Channel {channel} (Ignored -- only for ASCII mode)");
        }

        /// <summary>
        ///     Set the soft-CR character (for output wordwrap)
        ///
        ///     Signature: int btuscr(int chan,char softcr);
        /// </summary>
        private void btuscr()
        {
            var channel = GetParameter(0);
            var character = GetParameter(1);
            _logger.Info($"Set soft-CR character {character:X2} on Channel {channel} (Ignored -- only for ASCII mode)");
        }
    }
}
