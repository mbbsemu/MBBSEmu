using System;

namespace MBBSEmu.Date
{
  /// <summary>
  ///   Clock implementation using the local system clock.
  /// </summary>
  public class SystemClock : IClock
  {
    public DateTime Now { get => DateTime.Now; }
  }
}
