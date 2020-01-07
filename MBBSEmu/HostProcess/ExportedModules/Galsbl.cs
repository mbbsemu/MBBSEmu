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
    public class Galsbl : ExportedModuleBase, IExportedModule
    {

        public Galsbl(MbbsModule module, PointerDictionary<UserSession> channelDictionary) : base(module, channelDictionary)
        {
            if(!Module.Memory.HasSegment((ushort)EnumHostSegments.Bturno))
                Module.Memory.AddSegment((ushort) EnumHostSegments.Bturno);
        }

        public ReadOnlySpan<byte> Invoke(ushort ordinal)
        {
            switch (ordinal)
            {
                case 72:
                    return bturno();
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
                default:
                    throw new ArgumentOutOfRangeException($"Unknown Exported Function Ordinal: {ordinal}");
            }

            return null;
        }

        public void SetState(CpuRegisters registers, ushort channelNumber)
        {
            Registers = registers;
            Module.Memory.SetWord((ushort)EnumHostSegments.UserNum, 0, channelNumber);
        }

        /// <summary>
        ///     8 digit + NULL GSBL Registration Number
        ///
        ///     Signature: char bturno[]
        ///     Result: DX == Segment containing bturno
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> bturno()
        {
            const string registrationNumber = "97771457\0";
            Module.Memory.SetArray((ushort)EnumHostSegments.Bturno, 0, Encoding.Default.GetBytes(registrationNumber));

            return new IntPtr16((ushort) EnumHostSegments.Bturno, 0).ToSpan();
        }

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
            //TODO -- Set callback for how characters should be processed
            Registers.AX = 0;
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
            Module.Memory.SetWord((ushort) EnumHostSegments.Status, 0, status);

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

            var bytesToRead = 0;
            if (max > ChannelDictionary[channelNumber].InputBuffer.Length)
                bytesToRead = (int) ChannelDictionary[channelNumber].InputBuffer.Length;
            else
                bytesToRead = max;

            var bytesRead = new byte[bytesToRead];
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
            var routineOffset = GetParameter(1);
            var routineSegment = GetParameter(2);

            ChannelDictionary[channel].CharacterInterceptor = new IntPtr16(routineSegment, routineOffset);

#if DEBUG
            _logger.Info($"Assigned Character Interceptor Routine {routineSegment:X4}:{routineOffset:X4} to Channel {channel}");
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
        ///     
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

            ChannelDictionary[channel].DataToClient.Write(Module.Memory.GetString(stringSegment, stringOffset).ToArray());
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

            ChannelDictionary[channel].DataToClient.Write(Module.Memory.GetString(stringSegment, stringOffset).ToArray());

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
    }
}
