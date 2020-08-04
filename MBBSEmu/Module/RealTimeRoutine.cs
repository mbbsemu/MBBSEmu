using MBBSEmu.Memory;
using System.Diagnostics;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Defines a Real Time Routine as registered by RTKICK, RTIHDLR, or INITASK
    /// </summary>
    public class RealTimeRoutine : IntPtr16
    {
        /// <summary>
        ///     The delay in seconds between routine execution
        /// </summary>
        public readonly ushort Delay;

        /// <summary>
        ///     Stopwatch to track the time elapsed since last execution
        /// </summary>
        public Stopwatch Elapsed;

        /// <summary>
        ///     Denotes if this task has been executed already
        ///
        ///     RTKICK methods are only executed once
        /// </summary>
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
