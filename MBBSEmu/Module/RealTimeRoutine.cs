using System.Diagnostics;
using MBBSEmu.Memory;

namespace MBBSEmu.Module
{
    public class RealTimeRoutine : IntPtr16
    {
        public readonly ushort Delay;
        public Stopwatch Elapsed;
        public bool Executed;

        public RealTimeRoutine(ushort segment, ushort offset, ushort delay = 0) : base(segment, offset)
        {
            Delay = delay;

            //Only routines registered with Rtkick will have a delay > 0
            if (delay > 0)
            {
                //Begin Counting as soon as the routine is created by Rtkick
                Elapsed = new Stopwatch();
                Elapsed.Start();
            }
        }
    }
}
