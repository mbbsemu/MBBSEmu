namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Class for the x87 FPU Status Register
    /// </summary>
    public class FpuStatusRegister
    {
        public ushort RegisterWord { get; set; }

        public void SetFlag(EnumFpuStatusFlags statusFlag)
        {
            RegisterWord = (ushort) (RegisterWord | (ushort) statusFlag);
        }

        public void ClearFlag(EnumFpuStatusFlags statusFlag)
        {
            RegisterWord = (ushort) (RegisterWord & ~(ushort) statusFlag);
        }

        public byte GetStackTop() => (byte) ((RegisterWord >> 11) & 0x7);

        public void SetStackTop(byte value)
        {
            RegisterWord &= unchecked((ushort)(~0x3800)); //Zero out the previous value
            RegisterWord |= (ushort)((value & 0x7) << 11); //Write new one
        } 

        public void PopStackTop()
        {
            var stackTop = GetStackTop();
            stackTop++;
            if (stackTop > 7)
                stackTop = 0;

            SetStackTop(stackTop);
        }

        public void PushStackTop()
        {
            var stackTop = GetStackTop();

            //If this causes the stack to overflow, set it back to the base (top) address
            unchecked
            {
                stackTop--;
            }

            if (stackTop > 7)
                stackTop = 7;

            SetStackTop(stackTop);
        }

        public FpuStatusRegister()
        {
            RegisterWord = 0;
            SetStackTop(7);
        }
    }
}
