using System;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     Reads the host's sessions for the Channels sidebar
    ///
    ///     The host worker adds and removes sessions in a plain Dictionary while the UI thread reads it, so a read
    ///     can catch it mid-change (a skipped or null entry, or a collection-modified exception). That is a passing
    ///     race in the host, not a UI failure, so it is retried and, failing that, the last good snapshot is kept.
    /// </summary>
    public static class SessionSnapshot
    {
        private const int Attempts = 3;

        public static IReadOnlyList<ChannelSession> Read(Func<IEnumerable<ChannelSession>> read, IReadOnlyList<ChannelSession> previous)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                try
                {
                    return read().Where(x => x != null).ToList();
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException)
                {
                    //Read during a concurrent change; try again
                }
            }

            return previous;
        }
    }
}
