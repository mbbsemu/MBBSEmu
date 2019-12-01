using System;
using Iced.Intel;
using MBBSEmu.Logging;
using NLog;

namespace MBBSEmu.CPU
{
    public class CpuRegisters
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));
        /*
         * General Registers
         */

        /// <summary>
        ///     AX Register
        /// </summary>
        public int AX { get; set; }

        /// <summary>
        ///     AX Low Byte
        /// </summary>
        public int AL
        {
            get => AX & 0xFF;
            set
            {
                AX &= 0xFF00;
                AX |= value;
            }
        }

        /// <summary>
        ///     AX High Byte
        /// </summary>
        public int AH
        {
            get => AX >> 8;
            set
            {
                AX &= 0x00FF;
                AX |= value << 8;
            }
        }

        /// <summary>
        ///     Base Register
        /// </summary>
        public int BX { get; set; }

        /// <summary>
        ///     BX Low Byte
        /// </summary>
        public int BL
        {
            get => BX & 0xFF;
            set
            {
                BX &= 0xFF00;
                BX |= value;
            }
        }

        /// <summary>
        ///     BX High Byte
        /// </summary>
        public int BH
        {
            get => BX >> 8;
            set
            {
                BX &= 0x00FF;
                BX |= value << 8;
            }
        }



        public int CX { get; set; }

        /// <summary>
        ///     CX Low Byte
        /// </summary>
        public int CL
        {
            get => CX & 0xFF;
            set
            {
                CX &= 0xFF00;
                CX |= value;
            }
        }

        /// <summary>
        ///     CX High Byte
        /// </summary>
        public int CH
        {
            get => CX >> 8;
            set
            {
                CX &= 0x00FF;
                CX |= value << 8;
            }
        }

        public int DX { get; set; }

        /// <summary>
        ///     DX Low Byte
        /// </summary>
        public int DL
        {
            get => DX & 0xFF;
            set
            {
                DX &= 0xFF00;
                DX |= value;
            }
        }

        /// <summary>
        ///     DX High Byte
        /// </summary>
        public int DH
        {
            get => DX >> 8;
            set
            {
                DX &= 0x00FF;
                DX |= value << 8;
            }
        }

        /// <summary>
        ///     Pointer Register
        /// </summary>
        public int SP { get; set; }
        /// <summary>
        ///     Base Register
        /// </summary>
        public int BP { get; set; }
        /// <summary>
        ///     Index Register
        /// </summary>
        public int SI { get; set; }
        /// <summary>
        ///     Index Register
        /// </summary>
        public int DI { get; set; }

        /*
         * Memory Segmentation and Segment Registers
         */

        /// <summary>
        ///     Data Segment
        /// </summary>
        public int DS { get; set; }

        /// <summary>
        ///     Extra Segment
        /// </summary>
        public int ES { get; set; }

        /// <summary>
        ///     Stack Segment
        ///     Default Destination for String Operations
        /// </summary>
        public int SS { get; set; }

        /// <summary>
        ///     Code Segment
        /// </summary>
        public int CS { get; set; }

        /// <summary>
        ///     Flags
        /// </summary>
        public int F { get; set; }

        /// <summary>
        ///     Instruction Pointer
        /// </summary>
        public int IP { get; set; }

        /// <summary>
        ///     Machine Status Word
        /// </summary>
        public int MSW { get; set; }

        public CpuRegisters()
        {
            _logger.Info("X86_16 Registers Initialized!");
        }

        /// <summary>
        ///     Gets Value based on the specified Register
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public int GetValue(Register register)
        {
            switch (register)
            {
                case Register.AX:
                    return AX;
                case Register.AL:
                    return AL;
                case Register.AH:
                    return AH;
                case Register.CL:
                    return CL;
                case Register.DL:
                    return DL;
                case Register.BL:
                    return BL;
                case Register.CH:
                    return CH;
                case Register.DH:
                    return DH;
                case Register.BH:
                    return BH;
                case Register.CX:
                    return CX;
                case Register.DX:
                    return DX;
                case Register.BX:
                    return BX;
                case Register.SP:
                    return SP;
                case Register.BP:
                    return BP;
                case Register.SI:
                    return SI;
                case Register.DI:
                    return DI;
                case Register.ES:
                    return ES;
                case Register.CS:
                    return CS;
                case Register.SS:
                    return SS;
                case Register.DS:
                    return DS;
                case Register.EIP:
                    return IP;
                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }

        public void SetValue(Register register, int value)
        {
            switch (register)
            {
                case Register.AX:
                    AX = value;
                    break;
                case Register.AL:
                    AL = value;
                    break;
                case Register.AH:
                    AH = value;
                    break;
                case Register.CL:
                    CL = value;
                    break;
                case Register.DL:
                    DL = value;
                    break;
                case Register.BL:
                    BL = value;
                    break;
                case Register.CH:
                    CH = value;
                    break;
                case Register.DH:
                    DH = value;
                    break;
                case Register.BH:
                    BH = value;
                    break;
                case Register.CX:
                    CX = value;
                    break;
                case Register.DX:
                    DX = value;
                    break;
                case Register.BX:
                    BX = value;
                    break;
                case Register.SP:
                    SP = value;
                    break;
                case Register.BP:
                    BP = value;
                    break;
                case Register.SI:
                    SI = value;
                    break;
                case Register.DI:
                    DI = value;
                    break;
                case Register.ES:
                    ES = value;
                    break;
                case Register.CS:
                    CS = value;
                    break;
                case Register.SS:
                    SS = value;
                    break;
                case Register.DS:
                    DS = value;
                    break;
                case Register.EIP:
                    IP = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(register), register, null);
            }
        }
    }
}
