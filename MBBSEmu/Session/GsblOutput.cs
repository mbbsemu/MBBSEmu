using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Session
{
    /// <summary>
    ///     Kind of output being sent to a channel, which decides how GSBL treats it
    /// </summary>
    public enum EnumGsblOutputKind
    {
        /// <summary>
        ///     Text queued by btuxmt(), discarded by btuclo()
        /// </summary>
        Text,

        /// <summary>
        ///     Text queued by btuxmn(), which btuclo() won't discard
        /// </summary>
        ClearProofText,

        /// <summary>
        ///     Echo of typed input, which is neither scanned for screen-pause markers nor counted for paging
        /// </summary>
        Echo,
    }

    /// <summary>
    ///     Why output to a channel is paused
    /// </summary>
    public enum EnumGsblOutputPause
    {
        None,

        /// <summary>
        ///     The user sent the XOFF character
        /// </summary>
        Xoff,

        /// <summary>
        ///     The page-pause prompt has been sent and a key is awaited
        /// </summary>
        Page,
    }

    /// <summary>
    ///     Port of the transmit half of GALGSBL's channel: screen-pause and clear-pause-counter
    ///     characters, page line counting and the page-pause prompt (st_xmit, st_lf, st_more, st_ers),
    ///     XON/XOFF (st_wait, waitky), and btuclo().
    ///
    ///     GSBL queues output and drains it as the channel can take it. MBBSEmu sends output as soon as
    ///     it's written, so output is only queued here while the channel is paused.
    /// </summary>
    public class GsblChannelOutput
    {
        private readonly SessionBase _session;

        private readonly Action<byte[]> _transmit;

        private readonly LinkedList<Block> _held = new LinkedList<Block>();

        /// <summary>
        ///     Length of the page-pause prompt that was sent, which is erased with backspaces on resume
        /// </summary>
        private int _promptLength;

        private record Block(byte[] Data, EnumGsblOutputKind Kind, bool Binary);

        /// <summary>
        ///     XON character, the only key that resumes output when set; 0 lets any key resume it (btuxnf)
        /// </summary>
        public byte XonCharacter { get; set; }

        /// <summary>
        ///     Lines per page; 0 turns paging off (btuxnf cnt, with a negative xoff)
        /// </summary>
        public int PageLength { get; private set; }

        /// <summary>
        ///     Page-pause prompt (btuxnf stg). With no prompt set, the screen-pause character only
        ///     starts a new page instead of pausing.
        /// </summary>
        public byte[] PausePrompt { get; private set; }

        /// <summary>
        ///     Lines output on the current page (GSBL pgcnt)
        /// </summary>
        public int PageLineCount { get; set; }

        /// <summary>
        ///     Page pauses are suspended until the next completed input line or btuclo()
        /// </summary>
        public bool NonStop { get; set; }

        /// <summary>
        ///     Output since the last completed input line hasn't started a new line yet, so the CR that
        ///     ends it isn't counted (GSBL F_ECHOOK)
        /// </summary>
        public bool OutputComplete { get; set; }

        /// <summary>
        ///     Why output is paused, if it is
        /// </summary>
        public EnumGsblOutputPause Pause { get; private set; }

        public bool IsPaused => Pause != EnumGsblOutputPause.None;

        /// <summary>
        ///     Output written while paused, waiting to be sent
        /// </summary>
        public bool HasHeldOutput
        {
            get
            {
                lock (_session.OutputLock)
                    return _held.Count > 0;
            }
        }

        public GsblChannelOutput(SessionBase session, Action<byte[]> transmit)
        {
            _session = session;
            _transmit = transmit;
            Reset();
        }

        /// <summary>
        ///     Restores the GSBL rstchn() defaults for the output settings
        /// </summary>
        public void ResetSettings()
        {
            lock (_session.OutputLock)
            {
                XonCharacter = 0;
                PageLength = 0;
                PausePrompt = null;
                PageLineCount = 0;
                NonStop = false;
            }
        }

        /// <summary>
        ///     Restores the defaults and discards any paused output
        /// </summary>
        public void Reset()
        {
            lock (_session.OutputLock)
            {
                ResetSettings();
                OutputComplete = false;
                Pause = EnumGsblOutputPause.None;
                _promptLength = 0;
                _held.Clear();
            }
        }

        /// <summary>
        ///     Sets the XON character and turns paging on or off (GSBL btuxnf). The XOFF character is
        ///     input, and is set on <see cref="GsblChannelInput"/>.
        /// </summary>
        /// <param name="xon">XON character, 0 to let any key resume</param>
        /// <param name="paging">Turns paging on</param>
        /// <param name="pageLength">Lines per page when paging is on</param>
        /// <param name="prompt">Page-pause prompt when paging is on</param>
        public void SetFlowControl(byte xon, bool paging, int pageLength, byte[] prompt)
        {
            lock (_session.OutputLock)
            {
                XonCharacter = xon;
                if (paging)
                {
                    //Turning paging on starts a fresh page
                    if (PageLength == 0)
                        PageLineCount = 0;

                    NonStop = false;
                    PageLength = pageLength;
                    PausePrompt = prompt;
                    return;
                }

                PageLength = 0;
                PageLineCount = 0;
                NonStop = false;
            }
        }

        /// <summary>
        ///     Sends output, or holds it while the channel is paused
        /// </summary>
        /// <param name="data">Output, after text variables and line breaking</param>
        /// <param name="kind">btuxmt(), btuxmn() or echo output</param>
        /// <param name="binary">GSBL binary output mode, where pause characters and lines aren't special</param>
        public void Write(byte[] data, EnumGsblOutputKind kind, bool binary)
        {
            if (data.Length == 0)
                return;

            lock (_session.OutputLock)
            {
                if (kind != EnumGsblOutputKind.Echo && !binary)
                    data = InsertPauseBeforeClearScreen(data);

                if (IsPaused)
                {
                    _held.AddLast(new Block(data, kind, binary));
                    return;
                }

                Transmit(data, kind, binary);
            }
        }

        /// <summary>
        ///     The XOFF character was received: stop output until a key resumes it
        /// </summary>
        public void PauseForXoff()
        {
            lock (_session.OutputLock)
            {
                if (!IsPaused)
                    Pause = EnumGsblOutputPause.Xoff;
            }
        }

        /// <summary>
        ///     A key was received while output is paused (GSBL waitky). The key is consumed either way.
        /// </summary>
        /// <returns>TRUE if output resumed</returns>
        public bool WaitKey(byte c)
        {
            lock (_session.OutputLock)
            {
                if (!IsPaused || _session.InputLockout)
                    return false;

                c &= 0x7F;
                if (c != XonCharacter && XonCharacter != 0)
                    return false;

                Resume();
                return true;
            }
        }

        /// <summary>
        ///     An input line was completed, which starts a new page (GSBL inpchr)
        /// </summary>
        public void LineCompleted()
        {
            lock (_session.OutputLock)
            {
                OutputComplete = true;
                NonStop = false;
                PageLineCount = 0;
            }
        }

        /// <summary>
        ///     Discards output queued by btuxmt(), keeping btuxmn() output and echo (GSBL btuclo)
        /// </summary>
        public void Clear()
        {
            lock (_session.OutputLock)
            {
                NonStop = false;
                OutputComplete = true;
                PageLineCount = 0;

                //btuclo() drops btuxmt() blocks from either end of the queue, stopping at a btuxmn()
                //block, so anything between the first and last btuxmn() blocks survives
                var first = _held.First;
                while (first != null && first.Value.Kind != EnumGsblOutputKind.ClearProofText)
                    first = first.Next;

                var node = _held.First;
                var keep = false;
                while (node != null)
                {
                    var next = node.Next;
                    if (node == first)
                        keep = true;

                    if (!keep && node.Value.Kind != EnumGsblOutputKind.Echo)
                        _held.Remove(node);

                    node = next;
                }

                node = _held.Last;
                while (node != null && node.Value.Kind != EnumGsblOutputKind.ClearProofText)
                {
                    var previous = node.Previous;
                    if (node.Value.Kind != EnumGsblOutputKind.Echo)
                        _held.Remove(node);
                    node = previous;
                }
            }
        }

        /// <summary>
        ///     Resumes output, erasing the page-pause prompt and sending what was held (GSBL st_ers, st_xmit)
        /// </summary>
        private void Resume()
        {
            var reportOutputEmpty = _held.Count > 0 || Pause == EnumGsblOutputPause.Page;

            if (Pause == EnumGsblOutputPause.Page)
            {
                if (_promptLength > 0)
                {
                    var erase = new byte[_promptLength];
                    Array.Fill(erase, (byte)'\b');
                    _transmit(erase);
                }

                _promptLength = 0;
                OutputComplete = true;
                PageLineCount = 0;
            }

            Pause = EnumGsblOutputPause.None;

            while (!IsPaused && _held.Count > 0)
            {
                var block = _held.First.Value;
                _held.RemoveFirst();
                Transmit(block.Data, block.Kind, block.Binary);
            }

            if (reportOutputEmpty && !IsPaused && _held.Count == 0)
                _session.ReportOutputEmpty();
        }

        /// <summary>
        ///     Sends output byte by byte as GSBL st_xmit/st_lf would, consuming the screen-pause and
        ///     clear-pause-counter characters, counting lines, and pausing when the page is full.
        ///     Whatever follows a pause is held.
        /// </summary>
        private void Transmit(byte[] data, EnumGsblOutputKind kind, bool binary)
        {
            if (binary)
            {
                _transmit(data);
                return;
            }

            var output = new MemoryStream(data.Length);
            for (var i = 0; i < data.Length; i++)
            {
                var c = data[i];

                if (kind == EnumGsblOutputKind.Echo)
                {
                    output.WriteByte(c);
                    if (c == _session.GsblInput.LineTerminator)
                        OutputComplete = true;
                    continue;
                }

                if (IsPauseCharacter(c))
                {
                    if (PageLineCount != 0)
                    {
                        PageLineCount = 0;
                        if (PausePrompt != null)
                        {
                            PauseForPage(output, data, i + 1, kind);
                            return;
                        }
                    }
                    continue;
                }

                if (IsClearPauseCounterCharacter(c))
                {
                    PageLineCount = 0;
                    continue;
                }

                output.WriteByte(c);

                if (c != '\r')
                {
                    if (c != '\n')
                        OutputComplete = false;
                    continue;
                }

                //GSBL follows each CR with the linefeed, then counts the line
                if (i + 1 < data.Length && data[i + 1] == '\n')
                    output.WriteByte(data[++i]);

                if (OutputComplete || _session.InputLockout || NonStop)
                    continue;

                PageLineCount++;
                if (PageLength != 0 && PageLength <= PageLineCount && PausePrompt != null &&
                    (PageLength != PageLineCount || MoreLinesPending(data, i + 1)))
                {
                    PageLineCount = 0;
                    PauseForPage(output, data, i + 1, kind);
                    return;
                }
            }

            if (output.Length > 0)
                _transmit(output.ToArray());
        }

        /// <summary>
        ///     Sends the output so far and the page-pause prompt, and holds the rest (GSBL st_more)
        /// </summary>
        private void PauseForPage(MemoryStream output, byte[] data, int next, EnumGsblOutputKind kind)
        {
            output.Write(PausePrompt);
            _transmit(output.ToArray());

            _promptLength = PausePrompt.Length;
            Pause = EnumGsblOutputPause.Page;

            if (next < data.Length)
                _held.AddFirst(new Block(data[next..], kind, false));
        }

        /// <summary>
        ///     Another line is waiting to be sent after this one (GSBL lincnt != 0)
        /// </summary>
        private bool MoreLinesPending(byte[] data, int next)
        {
            if (Array.IndexOf(data, (byte)'\r', next) >= 0)
                return true;

            foreach (var block in _held)
            {
                if (block.Kind != EnumGsblOutputKind.Echo && !block.Binary && Array.IndexOf(block.Data, (byte)'\r') >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     The screen-pause character goes ahead of every clear screen (form feed or ESC [ 2 J), so
        ///     clearing the screen pauses a partly filled page (GSBL btuxmt)
        /// </summary>
        private byte[] InsertPauseBeforeClearScreen(byte[] data)
        {
            if (!_session.ScreenPauseEnabled || _session.PauseCharacter == 0)
                return data;

            MemoryStream result = null;
            var copied = 0;
            for (var i = 0; i < data.Length; i++)
            {
                var clearScreen = data[i] == '\f' ||
                                  (data[i] == 0x1B && i + 3 < data.Length && data[i + 1] == '[' && data[i + 2] == '2' && data[i + 3] == 'J');
                if (!clearScreen)
                    continue;

                result ??= new MemoryStream(data.Length + 4);
                result.Write(data, copied, i - copied);
                result.WriteByte(_session.PauseCharacter);
                copied = i;
            }

            if (result == null)
                return data;

            result.Write(data, copied, data.Length - copied);
            return result.ToArray();
        }

        private bool IsPauseCharacter(byte c) =>
            _session.ScreenPauseEnabled && _session.PauseCharacter != 0 && c == _session.PauseCharacter;

        private bool IsClearPauseCounterCharacter(byte c) =>
            _session.ClearPauseCounterCharacter != 0 && c == _session.ClearPauseCounterCharacter;

        /// <summary>
        ///     Screen-pause markers are consumed rather than sent, so they take no column when lines are broken
        /// </summary>
        public bool IsScreenPauseMarker(byte c) => IsPauseCharacter(c) || IsClearPauseCounterCharacter(c);
    }
}
