using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Server;
using MBBSEmu.Session;
using NLog;
using System;
using System.Text;
using System.Threading;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Class which defines functions &amp; properties that are part of the Galacticomm
    ///     Global Software Breakout Library (GALGSBL.H).
    /// </summary>
    public class Galgsbl : ExportedModuleBase, IExportedModule, IStoppable
    {
        /// <summary>
        ///     Segment Identifier for Relocation
        /// </summary>
        /// <returns></returns>
        public const ushort Segment = 0xFFFE;

        private ushort MonitoredChannel { get; set; }

        private ushort MonitoredChannel2 { get; set; }

        private readonly DateTime _startDate;
        private readonly Timer _timer;

        private const ushort ERROR_CHANNEL_NOT_DEFINED = 0xFFF6;
        private const ushort ERROR_CHANNEL_OUT_OF_RANGE = 0xFFF5;

        public Galgsbl(IClock clock, ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary) : base(
            clock, logger, configuration, fileUtility, globalCache, module, channelDictionary)
        {
            _startDate = clock.Now;
            Module.Memory.AllocateVariable("BTURNO", 9);

            //Check for Module Specific BTURNO #
            var bturno = configuration.GSBLBTURNO;
            if (!string.IsNullOrEmpty(_configuration.GetBTURNO(Module.ModuleIdentifier)))
            {
                bturno = _configuration.GetBTURNO(Module.ModuleIdentifier);
                _logger.Info($"Found Module Specific BTURNO # for {Module.ModuleIdentifier}. Setting BTURNO to: {bturno}");
            }

            //Sanity Check
            if (bturno.Length > 8)
                bturno = bturno.Substring(0, 8);

            Module.Memory.SetArray("BTURNO", Encoding.ASCII.GetBytes($"{bturno}\0"));
            Module.Memory.AllocateVariable("TICKER", 0x02); //ushort increments once per second

            MonitoredChannel2 = 0xFFFF;
            MonitoredChannel = 0xFFFF;

            TimeSpan timeSpan = TimeSpan.FromSeconds(1);
            _timer = new Timer(OnTimerCallback, this, timeSpan, timeSpan);
        }

        public void Stop()
        {
            _timer.Dispose();
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
        private void OnTimerCallback(object unused)
        {
            // Update TICKER. Wrapped in TryGetVariablePointer since debug tests might take too long
            // and trigger a race condition where the timer callback fires even though the test has
            // completed. This crashes the test.
            if (Module.Memory.TryGetVariablePointer("TICKER", out var tickerPointer))
            {
                var seconds = (ushort)((_clock.Now - _startDate).TotalSeconds % 0xFFFF);
                Module.Memory.SetWord(tickerPointer, seconds);
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
                var methodPointer = new FarPtr(0xFFFE, ordinal);
#if DEBUG
                //_logger.Debug($"Returning Method Offset {methodPointer.Segment:X4}:{methodPointer.Offset:X4}");
#endif
                return methodPointer.Data;
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
                case 26:
                    btulok();
                    break;
                case 22:
                    btuinp();
                    break;
                case 62:
                    chiinp();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal in GALGSBL: {ordinal}");
            }

            return null;
        }

        public void SetState(ushort channelNumber)
        {
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("USRNUM"), channelNumber);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> bturno() => Module.Memory.GetVariablePointer("BTURNO").Data;

        /// <summary>
        ///     Report the amount of space (number of bytes) available in the output buffer
        ///     Since we're not using a dialup terminal or any of that, we'll just set it to OUTBUF_SIZE
        ///
        ///     Signature: int btuoba(int chan)
        ///     Result: AX == bytes available
        /// </summary>
        /// <returns></returns>
        public void btuoba()
        {
            Registers.AX = OUTBUF_SIZE - 1;
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
            var channelNumber = GetParameter(0);
            var numBytes = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            if (numBytes == 0)
            {
                //Default ASCII mode -- we don't need to do anything
                channel.TransparentMode = false;
                Registers.AX = 0;
                return;
            }

            if (numBytes >= 1)
            {
                channel.TransparentMode = true;
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
            var channelNumber = GetParameter(0);
            var status = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            //Status Change
            //Set the Memory Value
            Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), status);

            //Notify the Session that a Status Change has occured
            channel.StatusChange = true;

#if DEBUG
            _logger.Debug($"Injecting Stauts {status} on channel {channelNumber}");
#endif

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

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            //Nothing to Input?
            if (channel.InputBuffer.Length == 0)
            {
                Registers.AX = 0;
                return;
            }

            int bytesToRead;
            if (max > channel.InputBuffer.Length)
                bytesToRead = (int)channel.InputBuffer.Length;
            else
                bytesToRead = max;

            var bytesRead = new byte[bytesToRead];
            channel.InputBuffer.Position = 0;
            channel.InputBuffer.Read(bytesRead, 0, bytesToRead);

            Module.Memory.SetArray(destinationSegment, destinationOffset, bytesRead);
            Registers.AX = (ushort)bytesToRead;
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

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.InputBuffer.SetLength(0);

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
                _logger.Debug($"Unassigned Character Interceptor Routine on Channel {channel}");
#endif
                return;
            }

            ChannelDictionary[channel].CharacterInterceptor = new FarPtr(routinePointer.Data);

#if DEBUG
            _logger.Debug($"Assigned Character Interceptor Routine {ChannelDictionary[channel].CharacterInterceptor} to Channel {channel}");
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
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            Registers.AX = (ushort)channel.InputBuffer.Length;
        }

        /// <summary>
        ///     String Output (via Echo Buffer)
        /// </summary>
        /// <returns></returns>
        public void chious()
        {
            var channelNumber = GetParameter(0);
            var stringPointer = GetParameterPointer(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.SendToClient(Module.Memory.GetString(stringPointer).ToArray());
        }

        /// <summary>
        ///     Transmit to channel (ASCIIZ string)
        ///
        ///     Signature: int btuxmt(int chan,char *datstg)
        /// </summary>
        public void btuxmt()
        {
            var channelNumber = GetParameter(0);
            var stringPointer = GetParameterPointer(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            var stringToSend = Module.Memory.GetString(stringPointer);
            var formattedString = FormatOutput(stringToSend);

            channel.SendToClient(formattedString.ToArray());

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
            var channelNumber = GetParameter(0);
            var onoff = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

#if DEBUG
            _logger.Debug($"EchoEmptyInvoke on Channel {channelNumber} == {onoff == 1}");
#endif

            channel.EchoEmptyInvokeEnabled = onoff == 1;
        }

        /// <summary>
        ///     Clears Command Input Buffer
        ///
        ///     Signature: int btuclc(int chan)
        /// </summary>
        private void btuclc()
        {
            var channelNumber = GetParameter(0);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.InputCommand = new byte[] { 0x0 };

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
            var channelNumber = GetParameter(0);
            var onoff = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            //Notify the Session that a Status Change has occured
            if (onoff == 1)
            {
                channel.OutputEmptyStatus = true;
                channel.StatusChange = true;
                Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), 5);
            }
            else
            {
                channel.OutputEmptyStatus = false;
                channel.StatusChange = true;
                Module.Memory.SetWord(Module.Memory.GetVariablePointer("STATUS"), 1);
            }

#if DEBUG
            _logger.Debug($"Value {onoff} for Channel {channelNumber}");
#endif

            Registers.AX = 0;
        }

        /// <summary>
        ///     Set Echo on/off
        ///
        ///     Signature: int btuech(int chan, int mode)
        /// </summary>
        private void btuech()
        {
            var channelNumber = GetParameter(0);
            var mode = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

#if DEBUG
            _logger.Debug($"Setting ECHO to: {mode} for channel {channelNumber}");
#endif
            channel.TransparentMode = mode == 0;
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
            var channelNumber = GetParameter(0);
            var messagePointer = GetParameterPointer(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            var messageToSend = Module.Memory.GetString(messagePointer);
            channel.SendToClient(messageToSend.ToArray());
        }

        private void btumon2()
        {
            var channelNumber = GetParameter(0);

            //Disable Monitoring
            if (channelNumber == 0xFFFF)
            {
#if DEBUG
                _logger.Debug($"Disabling Monitoring on all Channels");
#endif
                foreach (var c in ChannelDictionary.Values)
                {
                    c.Monitored = false;
                }

                MonitoredChannel2 = 0xFFFF;
                Registers.AX = 0;
            }

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.Monitored2 = true;
            MonitoredChannel2 = channelNumber;
            Registers.AX = 0;
#if DEBUG
            _logger.Debug($"Enabled Monitoring on Channel {channelNumber}");
#endif
        }

        private void btumks2()
        {
            var character = GetParameter(0);

            if (MonitoredChannel2 == 0xFFFF)
                return;

            ChannelDictionary[MonitoredChannel2].LastCharacterReceived = (byte)character;
            ChannelDictionary[MonitoredChannel2].InputBuffer.WriteByte((byte)character);
            ChannelDictionary[MonitoredChannel2].DataToProcess = true;
        }

        private ReadOnlySpan<byte> ticker => Module.Memory.GetVariablePointer("TICKER").Data;

        /// <summary>
        ///     Status of a Channel
        ///
        ///     Signature: int btusts(int chan)
        /// </summary>
        private void btusts()
        {
            var channelNumber = GetParameter(0);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            if (channel.DataToProcess && channel.Status == 3)
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
            var channelNumber = GetParameter(0);
            var character = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.PromptCharacter = (byte)character;
            Registers.AX = 0;
        }


        /// <summary>
        ///     This routine controls functions of the UART and (if used) the modem on a channel
        ///
        ///     Signature: int btucmd(int chan,char *cmdstg);
        /// </summary>
        private void btucmd()
        {
            var channelNumber = GetParameter(0);
            var command = GetParameterPointer(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            var commandString = Encoding.ASCII.GetString(Module.Memory.GetString(command, true));



            switch (commandString)
            {
                case "[":
                    _logger.Info($"Enable ANSI on channel {channelNumber} (Ignored)");
                    return;
                case "]":
                    _logger.Info($"Disable ANSI on channel {channelNumber} (Ignored)");
                    return;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported BTUCMD: {channelNumber}");
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

            ChannelDictionary[channel].SendToClient(new byte[] { (byte)byteToSend });
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

            Module.Memory.SetWord("STATUS", status);

#if DEBUG
            _logger.Debug($"Injecting Status {status} on Channel {channel}");
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
            _logger.Debug($"Set hard-CR character {character:X2} on Channel {channel} (Ignored -- only for ASCII mode)");
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
            _logger.Debug($"Set soft-CR character {character:X2} on Channel {channel} (Ignored -- only for ASCII mode)");
        }

        /// <summary>
        ///     Set input lockout on/off
        ///
        ///     Signature: int btulok(int chan,int onoff);
        /// </summary>
        private void btulok()
        {
            var channelNumber = GetParameter(0);
            var onoff = GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

#if DEBUG
            _logger.Debug($"Set InputLockout on channel {channelNumber} to {onoff == 1}");
#endif

            channel.InputLockout = onoff == 1;

        }

        /// <summary>
        ///     Input from a channel (ASCIIZ string)
        ///
        ///     Copies the entire input command from the input buffer to the specified destination pointer.
        ///     The information in the Input Buffer is then cleared.
        ///
        ///     The line terminator for the command is replaced by a NULL (\0), but since we don't write the newline
        ///     in SessionBase, we just NULL terminate the input.
        ///

        /// </summary>
        private void btuinp()
        {
            var channelNumber = GetParameter(0);
            var destinationPointer = GetParameterPointer(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            //Write to Destination
            Registers.AX = (ushort)channel.InputBuffer.Length;
            channel.InputBuffer.WriteByte(0); //NULL Terminate
            Module.Memory.SetArray(destinationPointer, channel.InputBuffer.ToArray());

            //Clear the Buffer
            channel.InputBuffer.SetLength(0);
        }

        /// <summary>
        ///     Takes the specified character and adds it directly to the channel input buffer
        ///
        ///     Signature: void chiinp(int chan,char c);
        /// </summary>
        private void chiinp()
        {
            var channelNumber = GetParameter(0);
            var character = (byte)GetParameter(1);

            if (!ChannelDictionary.TryGetValue(channelNumber, out var channel))
            {
                Registers.AX = ERROR_CHANNEL_NOT_DEFINED;
                return;
            }

            channel.InputBuffer.WriteByte(character);
        }
    }
}
