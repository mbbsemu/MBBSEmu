using System;

namespace MBBSEmu.Date
{
  /// <summary>
  ///   An interface to return the current date/time.
  /// </summary>
  public interface IClock
  {
    DateTime Now { get; }

    double CurrentTick { get; }
  }
}
