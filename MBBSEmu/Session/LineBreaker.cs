using System;

namespace MBBSEmu.Session
{
    public class LineBreaker
    {
        public const byte ESCAPE = 0x1B;
        public const int MAX_LINE = 512;
        public const int MAX_OUTPUT_BUFFER = 4096;

        public delegate void SendToClientDelegate(byte[] dataToSend);

        public SendToClientDelegate SendToClientMethod { get; set; }

        /// <summary>
        /// How many columns are supported by the client's terminal.
        /// </summary>
        public int TerminalColumns { get; set; }

        private enum ParseState {
            APS_NORMAL,
            APS_ESCAPE,
            APS_ESCAPE_BRACKET,
        }


        private ParseState _parseState = ParseState.APS_NORMAL;

        /// <summary>
        /// Contains all the characters accumulated by parsing outgoing data,
        /// for keeping track of cursor position.
        ///
        /// Does not contain any ANSI characters - these are filtered out.
        /// </summary>
        private readonly byte[] lineBuffer = new byte[MAX_LINE];
        /// <summary>
        /// How many characters are valid in lineBuffer
        /// </summary>
        private int lineBufferLength = 0;

        /// <summary>
        /// A mapping between lineBuffer to rawBuffer.
        ///
        /// Each index in this array (which is a printable character) maps to
        /// the data area where the character is in rawBuffer.
        /// </summary>
        private readonly int[] lineBufferToRawBuffer = new int[MAX_LINE];

        /// <summary>
        /// Contains all the bytes accumulated by parsing outgoing data,
        /// including ANSI characters. This array is output to our clients.
        /// </summary>
        private readonly byte[] rawBuffer = new byte[MAX_OUTPUT_BUFFER];
        /// <summary>
        /// How many bytes are valid in rawBuffer
        /// </summary>
        private int rawBufferLength = 0;

        /// <summary>
        /// Breaks buffer into lines and calls SendToClientMethod afterwards.
        /// </summary>
        /// <param name="buffer">Raw output buffer going to a client</param>
        public void SendBreakingIntoLines(byte[] buffer)
        {
            foreach(var b in buffer)
            {
                rawBuffer[rawBufferLength++] = b;

                switch (_parseState)
                {
                     case ParseState.APS_ESCAPE when b == '[':
                        _parseState = ParseState.APS_ESCAPE_BRACKET;
                        break;
                     case ParseState.APS_ESCAPE when b != ESCAPE:
                        _parseState = ParseState.APS_NORMAL;  // this happens if we receive an escape not followed by [, which shouldn't happen
                        break;
                    case ParseState.APS_ESCAPE_BRACKET when char.IsLetter((char) b): // ANSI tags are terminated with letters
                        _parseState = ParseState.APS_NORMAL;
                        break;
                    case ParseState.APS_NORMAL:
                        switch (b)
                        {
                            case (byte) '\r':
                                lineBufferLength = 0; // line ended, erase accumulated data in lineBuffer
                                break;
                            case ESCAPE: // escape
                                _parseState = ParseState.APS_ESCAPE;
                                break;
                            case (byte) '\n':  // ignore
                                break;
                            default:
                                lineBuffer[lineBufferLength] = b;
                                lineBufferToRawBuffer[lineBufferLength++] = rawBufferLength - 1;
                                // overflow to the next line
                                if (lineBufferLength > TerminalColumns)
                                {
                                    doLineBreak(b);
                                }
                                break;
                        }
                        break;
                    default:
                        // just eat up the character, often used when in APS_ESCAPE_BRACKET when waiting for the ending ANSI tag
                        break;
                }
            }

            // output our raw buffer
            SendToClientMethod(rawBuffer.AsSpan().Slice(0, rawBufferLength).ToArray());
            rawBufferLength = 0;
        }

        /// <summary>
        /// Performs a line break
        /// </summary>
        /// <param name="b">The character following the full line</param>
        private void doLineBreak(byte b)
        {
            if (char.IsWhiteSpace((char) b)) {
                // erase the space from the raw buffer and replace with \r\n
                rawBuffer[rawBufferLength - 1] = (byte) '\r';
                rawBuffer[rawBufferLength++] = (byte) '\n';
                // erase line build-up
                lineBufferLength = 0;
            }
            else
            {
                // scan for the last space to know where to break the line
                var lastSpaceIndex = lineBufferLength - 1;
                while ((lastSpaceIndex >= 0) && !char.IsWhiteSpace((char) lineBuffer[lastSpaceIndex]))
                {
                    --lastSpaceIndex;
                }

                if (lastSpaceIndex < 0)
                {
                    // no spaces = one huge line (shouldn't really happen), just break it up by inserting \r\n
                    rawBuffer[rawBufferLength - 1] = (byte) '\r';
                    rawBuffer[rawBufferLength] = (byte) '\n';
                    rawBuffer[rawBufferLength + 1] = b;

                    lineBuffer[0] = b;
                    lineBufferToRawBuffer[0] = rawBufferLength + 1;
                    lineBufferLength = 1;

                    rawBufferLength  += 2;
                }
                else
                {
                    // i is the location to place the \r\n
                    var insertIndex = lineBufferToRawBuffer[lastSpaceIndex];
                    var moveLen = rawBufferLength - insertIndex;
                    // shift stuff up to make room for \n
                    if (moveLen <= 0) {
                        throw new InvalidOperationException("Whoops");
                    }

                    Array.Copy(rawBuffer, insertIndex, rawBuffer, insertIndex + 1, moveLen);
                    // now insert, this destroys the space previously there and
                    // replaces with \r\n
                    rawBuffer[insertIndex + 0] = (byte) '\r';
                    rawBuffer[insertIndex + 1] = (byte) '\n';
                    ++rawBufferLength;

                    // setup the new line buffer
                    var chars_to_adjust = lineBufferLength - (lastSpaceIndex + 1);
                    int j, k;

                    Array.Copy(lineBuffer, lastSpaceIndex + 1, lineBuffer, 0, chars_to_adjust);

                    // adjust the raw pointers since we increased the pointer
                    // above
                    for (k = 0, j = (lastSpaceIndex + 1); j < lineBufferLength; ++j, ++k)
                    {
                        // copy entire thing
                        lineBufferToRawBuffer[k] = lineBufferToRawBuffer[j];
                        // increase the raw pointer
                        ++lineBufferToRawBuffer[k];
                    }
                    lineBufferLength = chars_to_adjust;
                }
            }
        }
    }
}