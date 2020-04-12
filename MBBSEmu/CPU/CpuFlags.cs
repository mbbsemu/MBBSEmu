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

        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, byte result = 0,
            byte destination = 0, byte source = 0)
        {
            bool setFlag;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    setFlag = (source + destination) > byte.MaxValue;
                    break;
                case EnumArithmeticOperation.Subtraction:
                    setFlag = result > destination;
                    break;
                case EnumArithmeticOperation.ShiftLeft:
                    setFlag = !result.IsNegative() && destination.IsNegative();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
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

        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, ushort result = 0,
            ushort destination = 0, ushort source = 0)
        {
            bool setFlag;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    setFlag = (source + destination) > ushort.MaxValue;
                    break;
                case EnumArithmeticOperation.Subtraction:
                    setFlag = result > destination;
                    break;
                case EnumArithmeticOperation.ShiftLeft:
                    setFlag = !result.IsNegative() && destination.IsNegative();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
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

        public void EvaluateCarry(EnumArithmeticOperation arithmeticOperation, uint result = 0,
            uint destination = 0, uint source = 0)
        {
            bool setFlag;
            switch (arithmeticOperation)
            {
                case EnumArithmeticOperation.Addition:
                    setFlag = ((ulong)source + destination) > uint.MaxValue;
                    break;
                case EnumArithmeticOperation.Subtraction:
                    setFlag = result > destination;
                    break;
                case EnumArithmeticOperation.ShiftLeft:
                    setFlag = !result.IsNegative() && destination.IsNegative();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(arithmeticOperation), arithmeticOperation,
                        "Unsupported Carry Flag Operation for Evaluation");
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
                    setFlag =  result.IsNegative();
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
