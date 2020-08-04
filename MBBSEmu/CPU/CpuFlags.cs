using MBBSEmu.Extensions;
using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Class that holds the CPU Status FLAGS Register and all associated functionality
    /// </summary>
    public class CpuFlags
    {
        /// <summary>
        ///     Flags Register Value
        /// </summary>
        public ushort Flags;

        /// <summary>
        ///     Evaluates the given 8-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, byte result = 0,
            byte destination = 0, byte source = 0)
        {
            var setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => (source + destination) > byte.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftLeft => !result.IsNegative() && destination.IsNegative(),
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
"Unsupported Carry Flag Operation for Evaluation"),
            };

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
        ///     Evaluates the given 16-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            bool setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => (source + destination) > ushort.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftLeft => !result.IsNegative() && destination.IsNegative(),
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                    "Unsupported Carry Flag Operation for Evaluation")
            };

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
        ///     Evaluates the given 32-bit operation and parameters to evaluate the status of the Carry Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, uint result = 0,
            uint destination = 0, uint source = 0)
        {
            bool setFlag = arithmeticOperation switch
            {
                EnumArithmeticOperation.Addition => ((ulong) source + destination) > uint.MaxValue,
                EnumArithmeticOperation.Subtraction => result > destination,
                EnumArithmeticOperation.ShiftLeft => !result.IsNegative() && destination.IsNegative(),
                _ => throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                    "Unsupported Carry Flag Operation for Evaluation")
            };

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
        ///     Evaluates the given 8-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, byte result = 0,
            byte destination = 0, byte source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
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
        ///     Evaluates the given 16-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
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
        ///     Evaluates the given 32-bit operation and parameters to evaluate the status of the Overflow Flag
        /// </summary>
        /// <param name="arithmeticOperation"></param>
        /// <param name="result"></param>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        public void EvaluateOverflow(EnumArithmeticOperation arithmeticOperation, uint result = 0,
            uint destination = 0, uint source = 0)
        {
            var setFlag = false;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    {
                        //positive+positive==negative
                        if (!destination.IsNegative() && !source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
                        }

                        //negative+negative==positive
                        if (destination.IsNegative() && source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        break;
                    }
                case EnumArithmeticOperation.Subtraction:
                    {

                        // negative-positive==positive
                        if (destination.IsNegative() && !source.IsNegative() &&
                            !result.IsNegative())
                        {
                            setFlag = true;
                        }

                        // positive-negative==negative
                        if (!destination.IsNegative() && source.IsNegative() &&
                            result.IsNegative())
                        {
                            setFlag = true;
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
        ///     Evaluates the given 8-bit result for Zero, Sign, and Parity Flags
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="result"></param>
        public void Evaluate(EnumFlags flag, byte result = 0)
        {
            bool setFlag;
            switch (flag)
            {
                case EnumFlags.ZF:
                    {
                        setFlag = result == 0;
                        break;
                    }
                case EnumFlags.SF:
                    {
                        setFlag = result.IsNegative();
                        break;
                    }
                case EnumFlags.PF:
                    {
                        setFlag = result.Parity();
                        break;
                    }

                //Unsupported Flags/Special Flags
                case EnumFlags.CF:
                    throw new Exception("Carry Flag must be evaluated with EvaluateCarry()");

                case EnumFlags.OF:
                    throw new Exception("Overflow must be evaluated with EvaluateOverflow()");

                //Everything Else
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

        /// <summary>
        ///     Evaluates the given 16-bit result for Zero, Sign, and Parity Flags
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="result"></param>
        public void Evaluate(EnumFlags flag, ushort result = 0)
        {
            bool setFlag;
            switch (flag)
            {
                case EnumFlags.ZF:
                    {
                        setFlag = result == 0;
                        break;
                    }
                case EnumFlags.SF:
                    {
                        setFlag = result.IsNegative();
                        break;
                    }
                case EnumFlags.PF:
                    {
                        setFlag = result.Parity();
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

        /// <summary>
        ///     Evaluates the given 32-bit result for Zero, Sign, and Parity Flags
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="result"></param>
        public void Evaluate(EnumFlags flag, uint result = 0)
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
                        setFlag = result.IsNegative();
                        break;
                    }
                case EnumFlags.PF:
                    {
                        setFlag = result.Parity();
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

        /// <summary>
        ///     Sets the bit for the specified Flag
        /// </summary>
        /// <param name="flag"></param>
        public void SetFlag(EnumFlags flag)
        {
            Flags = Flags.SetFlag((ushort)flag);
        }

        /// <summary>
        ///     Clears the bit for the specified Flag
        /// </summary>
        /// <param name="flag"></param>
        public void ClearFlag(EnumFlags flag)
        {
            Flags = Flags.ClearFlag((ushort)flag);
        }

        /// <summary>
        ///     Returns if the bit for the specified Flag is set
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public bool IsFlagSet(EnumFlags flag)
        {
            return Flags.IsFlagSet((ushort)flag);
        }

    }
}