using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     A user session as the Channels sidebar needs it
    /// </summary>
    public sealed record ChannelSession(int Channel, string Username);

    /// <summary>
    ///     One row of the Channels sidebar
    /// </summary>
    public sealed record ChannelLine(string Text, bool IsDisconnected);

    /// <summary>
    ///     Builds the Channels sidebar contents
    /// </summary>
    public static class ChannelList
    {
        /// <summary>
        ///     One line per configured channel: "N: USERNAME", "N: CONNECTING" (session without a username yet)
        ///     or "N: DISCONNECTED". Sessions on channels beyond channelCount are not shown.
        /// </summary>
        public static IReadOnlyList<ChannelLine> Build(int channelCount, IReadOnlyList<ChannelSession> sessions)
        {
            var lines = new ChannelLine[channelCount];
            for (var channel = 0; channel < channelCount; channel++)
            {
                var session = sessions.FirstOrDefault(x => x.Channel == channel);

                lines[channel] = session switch
                {
                    null => new ChannelLine($"{channel}: DISCONNECTED", true),
                    _ when string.IsNullOrEmpty(session.Username) => new ChannelLine($"{channel}: CONNECTING", false),
                    _ => new ChannelLine($"{channel}: {session.Username.ToUpperInvariant()}", false)
                };
            }

            return lines;
        }

        /// <summary>
        ///     Number of configured channels with a logged-in user
        /// </summary>
        public static int CountOnline(int channelCount, IReadOnlyList<ChannelSession> sessions) =>
            sessions.Count(x => x.Channel >= 0 && x.Channel < channelCount && !string.IsNullOrEmpty(x.Username));

        public static string Title(int online, int total) => $"Channels ({online}/{total})";
    }
}
