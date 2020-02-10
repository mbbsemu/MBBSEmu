using MBBSEmu.CPU;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Text;

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

        public Galgsbl(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
        {
            var bturnoPointer = Module.Memory.AllocateVariable("BTURNO", 9);
            Module.Memory.SetArray(bturnoPointer, Encoding.ASCII.GetBytes($"{_configuration["GSBL.Activation"]}\0"));

        }

        public void UpdateSession(ushort channel)
        {

        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
        {
            switch (ordinal)
            {
                case 72:
                    return bturno();
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
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal: {ordinal}");
            }

            return null;
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            Registers = registers;
            Module.Memory.SetWord(Module.Memory.GetVariable("USERNUM"), channelNumber);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> bturno() => Module.Memory.GetVariable("BTURNO").ToSpan();

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
            Registers.AX = ushort.MaxValue;
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
            Module.Memory.SetWord(Module.Memory.GetVariable("STATUS"), status);

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

            ChannelDictionary[channel].DataToClient.Enqueue(Module.Memory.GetString(stringSegment, stringOffset).ToArray());
        }

        /// <summary>
        ///     Transmit to channel (ASCIIZ string)
        ///
        ///     Signature: int btuxmt(int chan,char *datstg)
        /// </summary>
        public void btuxmt()
        {
            var channel = GetParameter(0);
            var stringOffset = GetParameter(1);
            var stringSegment = GetParameter(2);

            ChannelDictionary[channel].DataToClient.Enqueue(Module.Memory.GetString(stringSegment, stringOffset).ToArray());

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

        private void btuoes()
        {
            var channel = GetParameter(0);
            var onoff = GetParameter(1);

            if(onoff == 1)
                throw new Exception("MBBSEmu doesn't support generating Status 5s when the output buffer is empty");

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
            ChannelDictionary[channel].DataToClient.Enqueue(messageToSend.ToArray());
        }
    }
}
