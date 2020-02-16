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
            ControlWord = 0x37F;
            StatusWord = 0;
            SetStackTop(7);
        }
    }
}
