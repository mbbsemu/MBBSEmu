using MBBSEmu.Extensions;
using System;
using Microsoft.VisualBasic.CompilerServices;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Class that holds the CPU Status FLAGS Register and all associated functionality
    /// </summary>
    public class CpuFlags
    {
        public ushort Flags;

        /// <summary>
        ///     Carry Flag is Evaluated Differently depending on the operation type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateCarry<T>(EnumArithmeticOperation arithmeticOperation, ushort result = 0, ushort destination = 0)
        {
            bool setFlag;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                {
                    if (typeof(T) == typeof(byte))
                        setFlag = ((byte) destination).IsNegative() && result < destination;
                    else
                        setFlag = destination.IsNegative() && result < destination;
                    break;
                }
                case EnumArithmeticOperation.Subtraction:
                {
                    if (typeof(T) == typeof(byte))
                        setFlag = !((byte)destination).IsNegative() && ((byte)result).IsNegative();
                    else
                        setFlag = !destination.IsNegative() && result.IsNegative();
                    break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation, "Unsupported Carry Flag Operation for Evaluation");
            }

            if (setFlag)
            {
                SetFlag(EnumFlags.CF);
            }
            else
            {
                ClearFlag(EnumFlags.CF);
            }
        }

        /// <summary>
        ///     Evaluates the Overflow Flag
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateOverflow<T>(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                {
                    //destination+source=result
                    if (typeof(T) == typeof(byte))
                    {
                        //positive+positive==negative
                        if (!((byte)destination).IsNegative() && !((byte) source).IsNegative() &&
                            ((byte) result).IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (((byte) destination).IsNegative() && ((byte) source).IsNegative() &&
                            !((byte) result).IsNegative())
                        {
                            setFlag = true;
                        }
                    }
                    else
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() && result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() && !result.IsNegative())
                        {
                            setFlag = true;
                        }
                    }

                    break;
                }
                case EnumArithmeticOperation.Subtraction:
                {
                    //destination-source=result
                    if (typeof(T) == typeof(byte))
                    {
                        // negative-positive==positive
                        if (((byte)destination).IsNegative() && !((byte) source).IsNegative() &&
                            !((byte) result).IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!((byte)destination).IsNegative() && ((byte) source).IsNegative() &&
                            ((byte) result).IsNegative())
                        {
                            setFlag = true;
                        }
                    }
                    else
                    {
                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() && !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() && result.IsNegative())
                        {
                            setFlag = true;
                        }
                    }

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
            }

            if (setFlag)
            {
                SetFlag(EnumFlags.OF);
            }
            else
            {
                ClearFlag(EnumFlags.OF);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="flag"></param>
        /// <param name="result"></param>
        public void Evaluate<T>(EnumFlags flag, ushort result = 0)
        {
            var setFlag = false;
            switch (flag)
            {
                case EnumFlags.ZF:
                {
                    setFlag = result == 0;
                    break;
                }
                case EnumFlags.SF:
                {
                    setFlag = typeof(T) == typeof(byte) ? ((byte) result).IsNegative() : result.IsNegative();
                    break;
                }
                case EnumFlags.PF:
                {
                    setFlag = typeof(T) == typeof(byte) ? ((byte) result).Parity() : result.Parity();
                    break;
                }

                //Unsupported Flags/Special Flags
                case EnumFlags.CF:
                    throw new Exception("Carry Flag must be evaluated with EvaluateCarry()");

                case EnumFlags.OF:
                    throw new Exception("Overflow must be evaluated with EvaluateOverflow()");
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
