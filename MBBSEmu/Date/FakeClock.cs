using System;

namespace MBBSEmu.Date
{
  /// <summary>
  ///   Fake clock implementation which supports changing Now.
  /// </summary>
  public class FakeClock : IClock
  {
    public DateTime Now { get; set; }

    public FakeClock()
    {
      Now = DateTime.Now;
    }
  }
}
