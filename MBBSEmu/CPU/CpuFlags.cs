using MBBSEmu.Extensions;
using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Class that holds the CPU Status FLAGS Register and all associated functionality
    /// </summary>
    public class CpuFlags
    {
        public ushort Flags;

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void Evaluate<T>(EnumFlags flag, ushort result = 0, ushort destination = 0, ushort source = 0)
        {
            var setFlag = false;
            switch (flag)
            {
                    
                case EnumFlags.ZF:
                {
                    setFlag = result == 0;
                    break;
                }

                case EnumFlags.CF:
                {
                    setFlag = source > destination;
                    break;
                }

                case EnumFlags.OF:
                {
                    if (typeof(T) == typeof(byte))
                        setFlag = ((byte)result).IsNegative() != ((byte)destination).IsNegative();
                    if (typeof(T) == typeof(ushort)) 
                        setFlag = result.IsNegative() != destination.IsNegative();
                    break;
                }

                case EnumFlags.SF:
                {
                    if (typeof(T) == typeof(byte))
                        setFlag = ((byte) result).IsNegative();
                    if (typeof(T) == typeof(ushort))
                        setFlag = result.IsNegative();
                    break;
                }

                case EnumFlags.PF:
                {
                    if (typeof(T) == typeof(byte))
                        setFlag = ((byte)result).Parity();
                    if (typeof(T) == typeof(ushort))
                        setFlag = result.Parity();
                    break;
                }

                case EnumFlags.AF:
                    break;
                case EnumFlags.TF:
                    break;
                case EnumFlags.IF:
                    break;
                case EnumFlags.DF:
                    break;
                case EnumFlags.IOPL1:
                    break;
                case EnumFlags.IOPL2:
                    break;
                case EnumFlags.NT:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(flag), flag, null);
            }

            if (setFlag)
            {
                SetFlag(flag);
            }
            else
            {
                ClearFlag(flag);
            }
        }

        public void SetFlag(EnumFlags flag)
        {
            Flags = Flags.SetFlag((ushort)flag);
        }

        public void ClearFlag(EnumFlags flag)
        {
            Flags = Flags.ClearFlag((ushort)flag);
        }

        public bool IsFlagSet(EnumFlags flag)
        {
            return Flags.IsFlagSet((ushort)flag);
        }

    }
}
