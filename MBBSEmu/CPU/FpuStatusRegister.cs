using System;
using Iced.Intel;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Class for the x87 FPU Status Register
    /// </summary>
    public class FpuStatusRegister
    {
        public ushort StatusWord { get; set; }

        public ushort ControlWord { get; set; }

        public void SetFlag(EnumFpuStatusFlags statusFlag)
        {
            StatusWord = (ushort) (StatusWord | (ushort) statusFlag);
        }

        public void ClearFlag(EnumFpuStatusFlags statusFlag)
        {
            StatusWord = (ushort) (StatusWord & ~(ushort) statusFlag);
        }

        public byte GetStackTop() => (byte) ((StatusWord >> 11) & 0x7);

        public void SetStackTop(byte value)
        {
            StatusWord &= unchecked((ushort)(~0x3800)); //Zero out the previous value
            StatusWord |= (ushort)((value & 0x7) << 11); //Write new one
        }

        /// <summary>
        ///     Returns the Pointer in the Stack Array for the Specified Index
        /// 
        ///     Example: If there are three values in the FPU stack and ST(0) is FpuStack[3],
        ///              GetStackPointer(Registers.ST1) will return FpuStack[2] for the value of ST(1).
        /// </summary>
        /// <param name="register"></param>
        /// <returns></returns>
        public int GetStackPointer(Register register)
        {
            var registerOffset = register switch
            {
                Register.ST0 => 0,
                Register.ST1 => 1,
                Register.ST2 => 2,
                Register.ST3 => 3,
                Register.ST4 => 4,
                Register.ST5 => 5,
                Register.ST6 => 6,
                Register.ST7 => 7,
                _ => throw new Exception($"Unsupported FPU Register: {register}")
            };

            return GetStackTop() - registerOffset;
        }

        public void PopStackTop()
        {
            var stackTop = GetStackTop();
            unchecked
            {
                stackTop--;
            }
            
            if (stackTop > 7)
                stackTop = 7;

            SetStackTop(stackTop);
        }

        public void PushStackTop()
        {
            var stackTop = GetStackTop();

            //If this causes the stack to overflow, set it back to the base (top) address
            unchecked
            {
                stackTop++;
            }

            if (stackTop > 7)
                stackTop = 0;

            SetStackTop(stackTop);
        }

        /// <summary>
        ///     Clears Exception Flags from Status and Control Words
        /// </summary>
        public void ClearExceptions()
        {
            StatusWord &= 0xFFC0;
            ControlWord &= 0xFFC0;
        }

        public FpuStatusRegister()
        {
            ControlWord = 0x37F;
            StatusWord = 0;
            SetStackTop(7);
        }
    }
}
