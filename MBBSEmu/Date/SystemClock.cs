using System;
using System.Diagnostics;

namespace MBBSEmu.Date
{
  /// <summary>
  ///   Clock implementation using the local system clock.
  /// </summary>
  public class SystemClock : IClock
  {
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DateTime Now => DateTime.Now;

    public double CurrentTick => (double)_stopwatch.ElapsedTicks / (double)Stopwatch.Frequency;
  }
}
