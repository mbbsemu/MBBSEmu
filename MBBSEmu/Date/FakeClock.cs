using System;

namespace MBBSEmu.Date
{
  /// <summary>
  ///   Fake clock implementation which supports changing Now.
  /// </summary>
  public class FakeClock : IClock
  {
    private DateTime _now = DateTime.Now;

    public DateTime Now
    {
      get => _now;
      set => _now = value;
    }
  }
}
