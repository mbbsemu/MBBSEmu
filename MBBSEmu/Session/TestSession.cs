using MBBSEmu.HostProcess;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace MBBSEmu.Session
{
    public class TestSession : SessionBase
    {
        private readonly BlockingCollection<byte> _data = new BlockingCollection<byte>();

        //Transcript of every byte sent to the client, independent of what tests
        //consume via GetLine/Drain — for byte-exact assertions over a whole session
        private readonly MemoryStream _capturedOutput = new MemoryStream();
        private readonly object _capturedOutputLock = new object();

        public TestSession(IMbbsHost host, ITextVariableService textVariableService, string moduleIdentifier = "MBBSEMU") : base(host, "test", EnumSessionState.EnteringModule, textVariableService)
        {
            SendToClientMethod = Send;
            OutputEnabled = true;

            CurrentModule = host?.GetModule(moduleIdentifier);

            SessionType = EnumSessionType.Test;

            Username = "Sysop";
            Email = "sysop@grnet.com";
        }

        public override void Stop() { }


        /// <summary>
        ///     Reads data from the module until a new line is received, and returns the line with
        ///     the line endings removed.
        /// </summary>
        /// <param name="timeout">Maximum time to wait before throwing a TimeoutException</param>
        public string GetLine(TimeSpan timeout)
        {
            return GetLine('\n', timeout).Trim('\r', '\n');
        }

        /// <summary>
        ///     Reads data from the module until endingCharacter is received, and returns all data
        ///     accumulated including endingCharacter
        /// </summary>
        /// <param name="endingCharacter">Character which aborts reading</param>
        /// <param name="timeout">Maximum time to wait before throwing a TimeoutException</param>
        public string GetLine(char endingCharacter, TimeSpan timeout)
        {
            var line = new MemoryStream(80);
            while (true)
            {
                if (!_data.TryTake(out var b, timeout))
                {
                    throw new TimeoutException("Timeout, module likely didn't output expected text");
                }

                line.WriteByte(b);

                if (b == endingCharacter)
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(line.ToArray());
        }

        /// <summary>
        ///     Sends data originating from the module to the connected session, for consumption by
        ///     the test.
        /// </summary>
        /// <param name="dataToSend"></param>
        public virtual void Send(byte[] dataToSend)
        {
            lock (_capturedOutputLock)
            {
                _capturedOutput.Write(dataToSend, 0, dataToSend.Length);
            }

            foreach (var b in dataToSend)
            {
                _data.Add(b);
            }
        }

        /// <summary>
        ///     Drains and returns all bytes currently buffered for the client, for
        ///     byte-exact assertions in tests.
        /// </summary>
        public byte[] DrainSentData() => DrainSentData(TimeSpan.FromMilliseconds(50));

        /// <summary>
        ///     Drains bytes buffered for the client until the stream stays quiet for
        ///     quietPeriod — for paced module output that arrives in bursts.
        /// </summary>
        public byte[] DrainSentData(TimeSpan quietPeriod)
        {
            var buffer = new MemoryStream();
            while (_data.TryTake(out var b, quietPeriod))
                buffer.WriteByte(b);
            return buffer.ToArray();
        }

        /// <summary>
        ///     Reads raw bytes from the module until the accumulated data contains pattern,
        ///     and returns everything read including the pattern. Unlike GetLine, no ASCII
        ///     decoding — bytes >= 0x80 survive intact for byte-exact assertions.
        /// </summary>
        /// <param name="pattern">Byte sequence to wait for</param>
        /// <param name="timeout">Overall deadline before throwing a TimeoutException</param>
        public byte[] ReadUntilPattern(byte[] pattern, TimeSpan timeout)
        {
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("pattern must be non-empty", nameof(pattern));

            var deadline = DateTime.UtcNow + timeout;
            var buffer = new MemoryStream();
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !_data.TryTake(out var b, remaining))
                    throw new TimeoutException(
                        $"Timeout waiting for pattern; received {buffer.Length} bytes: " +
                        $"\"{Encoding.ASCII.GetString(buffer.ToArray())}\"");

                buffer.WriteByte(b);

                if (b == pattern[^1] && buffer.Length >= pattern.Length && EndsWith(buffer, pattern))
                    return buffer.ToArray();
            }
        }

        private static bool EndsWith(MemoryStream buffer, byte[] pattern)
        {
            var data = buffer.GetBuffer();
            var offset = buffer.Length - pattern.Length;
            for (var i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        ///     Snapshot of every byte sent to the client since the session started,
        ///     regardless of what GetLine/Drain calls have consumed.
        /// </summary>
        public byte[] GetCapturedOutput()
        {
            lock (_capturedOutputLock)
            {
                return _capturedOutput.ToArray();
            }
        }

        /// <summary>
        ///     Sends client data to the module.
        /// </summary>
        /// <param name="dataToSend"></param>
        public void SendToModule(byte[] dataToSend)
        {
            foreach (var b in dataToSend)
            {
                DataFromClient.Add(b);
            }
        }
    }
}
