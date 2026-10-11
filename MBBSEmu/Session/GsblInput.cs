using MBBSEmu.HostProcess.Enums;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Session
{
    /// <summary>
    ///     Per-channel GSBL input settings and line-assembly state, mirroring the input half of
    ///     GALGSBL's channel control block. Defaults match GSBL rstchn().
    /// </summary>
    public class GsblChannelInput
    {
        /// <summary>
        ///     Unlimited line length (btumil(chan, 0))
        /// </summary>
        public const int UnlimitedLineLength = 0x10000;

        /// <summary>
        ///     Echo typed characters back to the channel (btuech)
        /// </summary>
        public bool EchoEnabled { get; set; }

        /// <summary>
        ///     Character echoed for a backspace; 0 disables backspace handling (btubse)
        /// </summary>
        public byte BackspaceCharacter { get; set; }

        /// <summary>
        ///     Input line terminator (btutrm)
        /// </summary>
        public byte LineTerminator { get; set; }

        /// <summary>
        ///     Character echoed after the line terminator; 0 disables it (btulfd)
        /// </summary>
        public byte LinefeedCharacter { get; set; }

        /// <summary>
        ///     Maximum input line length (btumil)
        /// </summary>
        public int MaxInputLength { get; set; }

        /// <summary>
        ///     Wrap the last word onto a new line at MaxInputLength instead of overflowing (btumil maxinl &lt; 0)
        /// </summary>
        public bool WordWrap { get; set; }

        /// <summary>
        ///     XOFF character, which pauses output and is never delivered as input; 0 disables it (btuxnf)
        /// </summary>
        public byte XoffCharacter { get; set; }

        /// <summary>
        ///     Output-abort character, never delivered as input; 0 disables it (btutru)
        /// </summary>
        public byte OutputAbortCharacter { get; set; }

        /// <summary>
        ///     Input byte trigger count; 0 is line mode (btutrg)
        /// </summary>
        public int Trigger { get; set; }

        /// <summary>
        ///     Bytes received toward the next trigger block
        /// </summary>
        public int TriggerCount { get; set; }

        /// <summary>
        ///     Last raw character received, used to discard LF following CR
        /// </summary>
        public byte LastCharacter { get; set; }

        /// <summary>
        ///     InputBuffer holds a completed line that hasn't been consumed yet. Input typed
        ///     meanwhile (for example, the word carried over by word wrap) goes to PendingLine.
        /// </summary>
        public bool LineComplete { get; set; }

        /// <summary>
        ///     The line being typed while InputBuffer still holds an unconsumed completed line
        /// </summary>
        public List<byte> PendingLine { get; } = new List<byte>();

        /// <summary>
        ///     PendingLine has been terminated, and becomes available once InputBuffer is consumed
        ///     (GSBL's deferred CR)
        /// </summary>
        public bool PendingLineComplete { get; set; }

        public GsblChannelInput()
        {
            Reset();
        }

        /// <summary>
        ///     Restores the GSBL rstchn() defaults for every setting
        /// </summary>
        public void Reset()
        {
            EchoEnabled = true;
            BackspaceCharacter = 0x08;
            LineTerminator = 0x0D;
            LinefeedCharacter = 0x0A;
            MaxInputLength = UnlimitedLineLength;
            WordWrap = false;
            XoffCharacter = 0x13;
            OutputAbortCharacter = 0;
            Trigger = 0;
            TriggerCount = 0;
        }

        /// <summary>
        ///     Discards any partially assembled input (btucli)
        /// </summary>
        public void ClearInput()
        {
            TriggerCount = 0;
            LineComplete = false;
            PendingLine.Clear();
            PendingLineComplete = false;
        }
    }

    public enum EnumGsblInputResult
    {
        /// <summary>
        ///     Character was consumed by the line discipline (stored, edited, or ended a line)
        /// </summary>
        Accepted,

        /// <summary>
        ///     Character was discarded (locked out, untranslatable, overflow, etc.)
        /// </summary>
        Dropped,

        /// <summary>
        ///     The btuchi() interceptor returned 0 for this character
        /// </summary>
        InterceptorDropped,
    }

    /// <summary>
    ///     Port of GALGSBL's line-mode input processing (inpchr, wrdwrp, chiinp). Each character
    ///     received from a channel is translated, edited and echoed according to the channel's
    ///     GSBL settings before it reaches the session InputBuffer.
    /// </summary>
    public static class GsblInput
    {
        private const string TranslationTableCacheKey = "GALGSBL-XLTTAB";

        /// <summary>
        ///     GSBL's default input translation table (btuxlt)
        ///
        ///     00-1F: control characters are dropped, except BS and CR; LF becomes CR
        ///     20-7F: printable ASCII is unchanged; DEL becomes BS
        ///     80-FF: as 00-7F with the 8th bit stripped, except that 8A (LF) is dropped
        /// </summary>
        public static ReadOnlySpan<byte> DefaultTranslationTable => new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x0d, 0x00, 0x00, 0x0d, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x2d, 0x2e, 0x2f,
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f,
            0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f,
            0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5e, 0x5f,
            0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x6f,
            0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e, 0x08,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x2d, 0x2e, 0x2f,
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f,
            0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f,
            0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5e, 0x5f,
            0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x6f,
            0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e, 0x08,
        };

        /// <summary>
        ///     The input translation table is global to GALGSBL (btuxlt changes it for every channel),
        ///     so it lives in the global cache, created from the default on first use.
        /// </summary>
        public static byte[] GetTranslationTable(IGlobalCache globalCache)
        {
            if (globalCache.TryGet<byte[]>(TranslationTableCacheKey, out var table))
                return table;

            table = DefaultTranslationTable.ToArray();
            globalCache.Set(TranslationTableCacheKey, table);
            return table;
        }

        /// <summary>
        ///     Once a completed line has been consumed from InputBuffer, moves the line typed in the
        ///     meantime into InputBuffer, reporting it if it was also completed. Called every tick.
        /// </summary>
        public static void Synchronize(SessionBase session)
        {
            var gsbl = session.GsblInput;

            if (!gsbl.LineComplete || session.InputBuffer.Length > 0)
                return;

            gsbl.LineComplete = false;
            if (gsbl.PendingLine.Count == 0 && !gsbl.PendingLineComplete)
                return;

            session.InputBuffer.Write(gsbl.PendingLine.ToArray());
            gsbl.PendingLine.Clear();

            if (gsbl.PendingLineComplete)
            {
                gsbl.PendingLineComplete = false;
                gsbl.LineComplete = true;
                PostLineAvailable(session);
            }
        }

        /// <summary>
        ///     Processes one character received from the channel (GSBL inpchr)
        /// </summary>
        /// <param name="session">Channel receiving the character</param>
        /// <param name="c">Raw character received</param>
        /// <param name="translationTable">GSBL input translation table, used when no interceptor is set</param>
        /// <param name="interceptor">btuchi() routine, which replaces the translation table when set</param>
        /// <param name="pageKeyHandler">btuhpk() routine, consulted for keys received during a page pause</param>
        public static EnumGsblInputResult ProcessCharacter(SessionBase session, byte c, byte[] translationTable, Func<byte, byte> interceptor,
            Func<byte, byte> pageKeyHandler = null)
        {
            var gsbl = session.GsblInput;

            if (session.InputLockout)
                return EnumGsblInputResult.Dropped;

            Synchronize(session);

            //While output is paused, every key is offered to resume it and is never input (GSBL chnodn, waitky)
            if (session.GsblOutput.IsPaused)
            {
                gsbl.LastCharacter = c;
                session.GsblOutput.WaitKey(c, pageKeyHandler);
                return EnumGsblInputResult.Dropped;
            }

            var previous = gsbl.LastCharacter;
            gsbl.LastCharacter = c;
            if (c == '\n' && previous == '\r')
                return EnumGsblInputResult.Dropped;

            //Trigger (block) mode: raw bytes, no translation, editing or echo
            if (gsbl.Trigger > 0)
            {
                session.InputBuffer.Seek(0, SeekOrigin.End);
                session.InputBuffer.WriteByte(c);
                if (++gsbl.TriggerCount >= gsbl.Trigger)
                {
                    gsbl.TriggerCount -= gsbl.Trigger;
                    session.Status.Enqueue(EnumUserStatus.INBLK);
                }
                return EnumGsblInputResult.Accepted;
            }

            var x = (byte)(c & 0x7F);
            if (x == gsbl.XoffCharacter)
            {
                if (x != 0)
                {
                    session.GsblOutput.PauseForXoff();
                    return EnumGsblInputResult.Dropped;
                }
            }
            else if (x == gsbl.OutputAbortCharacter)
            {
                //Output is only queued while paused, and then every key goes to WaitKey, so there's
                //never queued output to abort here
                c = 0;
            }

            if (interceptor != null)
            {
                c = interceptor(c);
                if (c == 0)
                    return EnumGsblInputResult.InterceptorDropped;
            }
            else
            {
                c = translationTable[c];
                if (c == 0)
                    return EnumGsblInputResult.Dropped;
            }

            session.CharacterProcessed = c;

            var echo = new MemoryStream();
            try
            {
                return ProcessTranslatedCharacter(session, c, interceptor != null, echo);
            }
            finally
            {
                if (echo.Length > 0)
                    session.SendEchoToClient(echo.ToArray());
            }
        }

        private static EnumGsblInputResult ProcessTranslatedCharacter(SessionBase session, byte c, bool intercepted, MemoryStream echo)
        {
            var gsbl = session.GsblInput;

            if (c == '\b' && gsbl.BackspaceCharacter != 0)
            {
                if (LineLength(session) == 0)
                    return EnumGsblInputResult.Dropped;

                RemoveLastCharacter(session);
                if (gsbl.EchoEnabled)
                {
                    echo.WriteByte(gsbl.BackspaceCharacter);
                    if (gsbl.BackspaceCharacter == c)
                    {
                        echo.WriteByte((byte)' ');
                        echo.WriteByte((byte)'\b');
                    }
                }
                return EnumGsblInputResult.Accepted;
            }

            if (c != '\b' && c == gsbl.LineTerminator)
                return CompleteLine(session, c, intercepted, echo);

            if (LineLength(session) >= gsbl.MaxInputLength)
            {
                if (!gsbl.WordWrap)
                    return Overflow(session);

                if (c == ' ')
                    return CompleteLine(session, gsbl.LineTerminator, intercepted, echo);

                if (!WrapWord(session, echo))
                    return Overflow(session);
            }

            //echsec(): MajorBBS limits secure input to the requested width
            if (session.EchoSecureEnabled && LineLength(session) >= session.ExtUsrAcc.wid)
                return EnumGsblInputResult.Dropped;

            if (!CanStoreCharacter(session))
                return Overflow(session);

            AppendCharacter(session, c);
            if (gsbl.EchoEnabled)
                echo.WriteByte(session.EchoSecureEnabled ? session.ExtUsrAcc.ech : c);

            return EnumGsblInputResult.Accepted;
        }

        /// <summary>
        ///     Driver line input (GSBL chiinp): line editing without translation or echo
        /// </summary>
        public static void ProcessDriverCharacter(SessionBase session, byte c)
        {
            Synchronize(session);

            var echo = new MemoryStream();
            ProcessDriverCharacter(session, c, echo);

            //Only word wrap echoes here
            if (echo.Length > 0)
                session.SendEchoToClient(echo.ToArray());
        }

        private static void ProcessDriverCharacter(SessionBase session, byte c, MemoryStream echo)
        {
            var gsbl = session.GsblInput;

            if (c == '\b' && gsbl.BackspaceCharacter != 0)
            {
                if (LineLength(session) > 0)
                    RemoveLastCharacter(session);
                return;
            }

            if (c != gsbl.LineTerminator)
            {
                //chiinp() allows one character past the limit
                if (LineLength(session) > gsbl.MaxInputLength)
                {
                    if (!gsbl.WordWrap)
                    {
                        Overflow(session);
                        return;
                    }

                    if (c != ' ')
                    {
                        if (!WrapWord(session, echo))
                        {
                            Overflow(session);
                            return;
                        }

                        AppendCharacter(session, c);
                        return;
                    }
                }
                else
                {
                    if (CanStoreCharacter(session))
                        AppendCharacter(session, c);
                    else
                        Overflow(session);
                    return;
                }
            }

            if (!TerminateLine(session))
            {
                Overflow(session);
                return;
            }

            session.GsblOutput.LineCompleted();
        }

        /// <summary>
        ///     Ends the current input line, echoing the terminator and linefeed if echo is on
        /// </summary>
        private static EnumGsblInputResult CompleteLine(SessionBase session, byte terminator, bool intercepted, MemoryStream echo)
        {
            var gsbl = session.GsblInput;

            //MBBSEmu compatibility (MajorMUD): with echo off, a CR received while the module is
            //cycling is left in the input buffer for STSROU instead of being reported to STTROU
            if (!gsbl.EchoEnabled && !intercepted && session.GetStatus() == EnumUserStatus.CYCLE)
            {
                AppendCharacter(session, terminator);
                return EnumGsblInputResult.Accepted;
            }

            if (!TerminateLine(session))
                return Overflow(session);

            session.GsblOutput.LineCompleted();

            if (gsbl.EchoEnabled)
            {
                echo.WriteByte(terminator);
                if (gsbl.LinefeedCharacter != 0)
                    echo.WriteByte(gsbl.LinefeedCharacter);
            }

            return EnumGsblInputResult.Accepted;
        }

        /// <summary>
        ///     Marks the line being typed as complete and reports it, deferring it if InputBuffer
        ///     still holds an earlier line. Returns false if a deferred line is already waiting.
        /// </summary>
        private static bool TerminateLine(SessionBase session)
        {
            var gsbl = session.GsblInput;

            if (gsbl.LineComplete)
            {
                if (gsbl.PendingLineComplete)
                    return false;

                gsbl.PendingLineComplete = true;
                return true;
            }

            gsbl.LineComplete = session.InputBuffer.Length > 0;
            PostLineAvailable(session);
            return true;
        }

        /// <summary>
        ///     Moves the last word of a full line onto a new line (GSBL wrdwrp). The space before it
        ///     ends the current line, which is reported as complete.
        /// </summary>
        private static bool WrapWord(SessionBase session, MemoryStream echo)
        {
            var gsbl = session.GsblInput;

            //The wrapped word would start a third line while two are still unconsumed
            if (gsbl.LineComplete)
                return false;

            var line = CurrentLine(session);

            //Scan back for a space; one in the first position doesn't count
            var space = line.Length - 1;
            while (space > 0 && line[space] != ' ')
                space--;

            if (space == 0)
            {
                //No space: echo a backspace, even with echo off
                echo.WriteByte((byte)'\b');
                return false;
            }

            var word = line[(space + 1)..];
            SetCurrentLine(session, line[..space]);
            TerminateLine(session);
            foreach (var b in word)
                AppendCharacter(session, b);

            if (gsbl.EchoEnabled)
            {
                for (var i = 0; i < word.Length; i++)
                    echo.WriteByte((byte)'\b');
                for (var i = 0; i < word.Length; i++)
                    echo.WriteByte((byte)' ');
                echo.WriteByte(gsbl.LineTerminator);
                if (gsbl.LinefeedCharacter != 0)
                    echo.WriteByte(gsbl.LinefeedCharacter);
                echo.Write(word);
            }

            return true;
        }

        /// <summary>
        ///     Reports an input overflow, unless the most recent status already does (GSBL ovflow)
        /// </summary>
        private static EnumGsblInputResult Overflow(SessionBase session)
        {
            if (session.Status.Count == 0 || session.Status.ToArray()[^1] != EnumUserStatus.INPUT_BUFFER_OVERFLOW)
                session.Status.Enqueue(EnumUserStatus.INPUT_BUFFER_OVERFLOW);

            return EnumGsblInputResult.Dropped;
        }

        private static void PostLineAvailable(SessionBase session)
        {
            //A CR received while cycling preempts the cycle so STTROU sees the input promptly
            if (session.GetStatus() == EnumUserStatus.CYCLE)
            {
                session.Status.Clear();
                session.EchoSecureEnabled = false;
            }

            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)
                session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);
        }

        /// <summary>
        ///     Two completed lines are already waiting to be consumed, so there's no room for a third
        /// </summary>
        private static bool CanStoreCharacter(SessionBase session) =>
            !(session.GsblInput.LineComplete && session.GsblInput.PendingLineComplete);

        private static int LineLength(SessionBase session)
        {
            if (!CanStoreCharacter(session))
                return 0;

            return session.GsblInput.LineComplete ? session.GsblInput.PendingLine.Count : (int)session.InputBuffer.Length;
        }

        private static byte[] CurrentLine(SessionBase session) =>
            session.GsblInput.LineComplete ? session.GsblInput.PendingLine.ToArray() : session.InputBuffer.ToArray();

        private static void SetCurrentLine(SessionBase session, byte[] line)
        {
            if (session.GsblInput.LineComplete)
            {
                session.GsblInput.PendingLine.Clear();
                session.GsblInput.PendingLine.AddRange(line);
                return;
            }

            session.InputBuffer.SetLength(0);
            session.InputBuffer.Write(line);
        }

        private static void AppendCharacter(SessionBase session, byte c)
        {
            if (session.GsblInput.LineComplete)
            {
                session.GsblInput.PendingLine.Add(c);
                return;
            }

            session.InputBuffer.Seek(0, SeekOrigin.End);
            session.InputBuffer.WriteByte(c);
        }

        private static void RemoveLastCharacter(SessionBase session)
        {
            if (session.GsblInput.LineComplete)
            {
                session.GsblInput.PendingLine.RemoveAt(session.GsblInput.PendingLine.Count - 1);
                return;
            }

            session.InputBuffer.SetLength(session.InputBuffer.Length - 1);
        }
    }
}
