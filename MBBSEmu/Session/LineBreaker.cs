using System.Collections.Generic;
using System;

namespace MBBSEmu.Session
{
    public class LineBreaker
    {
        public const byte BACKSPACE = 8;
        public const byte DELETE = 127;
        public const byte ESCAPE = 0x1B;
        public const int MAX_LINE = 512;
        public const int MAX_OUTPUT_BUFFER = 16 * 1024;

        public delegate void SendToClientDelegate(byte[] dataToSend);

        public SendToClientDelegate SendToClientMethod { get; set; }

        /// <summary>
        /// How many columns are supported by the client's terminal.
        /// </summary>
        public int WordWrapWidth { get; set; }

        private enum ParseState {
            NORMAL,
            ESCAPE,
            BRACKET,
            VALUE_ACCUM,
            WAIT_FOR_ANSI_END
        }


        private ParseState _parseState = ParseState.NORMAL;

        /// <summary>
        /// Contains all the characters accumulated by parsing outgoing data,
        /// for keeping track of cursor position.
        ///
        /// Does not contain any ANSI characters - these are filtered out.
        /// </summary>
        private readonly byte[] _lineBuffer = new byte[MAX_LINE];
        /// <summary>
        /// How many characters are valid in lineBuffer
        /// </summary>
        private int _lineBufferLength = 0;

        /// <summary>
        /// A mapping between lineBuffer to rawBuffer.
        ///
        /// Each index in this array (which is a printable character) maps to
        /// the data area where the character is in rawBuffer.
        /// </summary>
        private readonly int[] _lineBufferToRawBuffer = new int[MAX_LINE];

        /// <summary>
        /// Contains all the bytes accumulated by parsing outgoing data,
        /// including ANSI characters. This array is output to our clients.
        /// </summary>
        private readonly byte[] _rawBuffer = new byte[MAX_OUTPUT_BUFFER];
        /// <summary>
        /// How many bytes are valid in rawBuffer
        /// </summary>
        private int _rawBufferLength = 0;

        private int _accumulator = 0;
        private List<int> _values = new List<int>();

        public void Reset()
        {
            _lineBufferLength = 0;
            _rawBufferLength = 0;
            _accumulator = 0;
            _values.Clear();
        }

        /// <summary>
        /// Breaks buffer into lines and calls SendToClientMethod afterwards.
        /// </summary>
        /// <param name="buffer">Raw output buffer going to a client</param>
        public void SendBreakingIntoLines(byte[] buffer)
        {
            foreach(var b in buffer)
            {
                _rawBuffer[_rawBufferLength++] = b;

                switch (_parseState)
                {
                    case ParseState.ESCAPE when b == '[':
                        _accumulator = 0;
                        _values.Clear();
                        _parseState = ParseState.BRACKET;
                        break;
                    case ParseState.ESCAPE when b != ESCAPE:
                        _parseState = ParseState.NORMAL;  // this happens if we receive an escape not followed by [, which shouldn't happen
                        break;
                    case ParseState.BRACKET when char.IsDigit((char) b):
                        _accumulator = ((char) b) - '0';
                        _parseState = ParseState.VALUE_ACCUM;
                        break;
                    case ParseState.BRACKET: // something else? how about waiting until an ending frame
                        _parseState = ParseState.WAIT_FOR_ANSI_END;
                        break;
                    case ParseState.WAIT_FOR_ANSI_END when b == ESCAPE:
                        _parseState = ParseState.ESCAPE;
                        break;
                    case ParseState.WAIT_FOR_ANSI_END when isAnsiSequenceFinished((char) b):
                        processAnsiCommand((char) b);
                        _parseState = ParseState.NORMAL;
                        break;
                    case ParseState.VALUE_ACCUM when b == ';':
                        _values.Add(_accumulator);
                        _accumulator = 0;
                        break;
                    case ParseState.VALUE_ACCUM when char.IsDigit((char) b):
                        _accumulator = 10 * _accumulator + (((char) b) - '0');
                        break;
                    case ParseState.VALUE_ACCUM when isAnsiSequenceFinished((char) b):
                        // push back last digit
                        if (_accumulator > 0) {
                            _values.Add(_accumulator);
                        }

                        processAnsiCommand((char) b);

                        _accumulator = 0;
                        _values.Clear();
                        _parseState = ParseState.NORMAL;
                        break;
                    case ParseState.NORMAL:
                        switch (b)
                        {
                            case (byte) '\r':
                                _lineBufferLength = 0; // line ended, erase accumulated data in lineBuffer
                                break;
                            case ESCAPE: // escape
                                _parseState = ParseState.ESCAPE;
                                break;
                            case DELETE:       // ignore
                            case (byte) '\n':  // ignore
                                break;
                            case BACKSPACE:
                                _lineBufferLength = Math.Max(0, _lineBufferLength - 1);
                                break;
                            default:
                                _lineBuffer[_lineBufferLength] = b;
                                _lineBufferToRawBuffer[_lineBufferLength++] = _rawBufferLength - 1;
                                // overflow to the next line
                                if (_lineBufferLength > WordWrapWidth)
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
            SendToClientMethod(_rawBuffer.AsSpan().Slice(0, _rawBufferLength).ToArray());
            _rawBufferLength = 0;
        }

        private void processAnsiCommand(char ansiCommand)
        {
            switch (ansiCommand)
            {
                // H resets position to 0,0 with no arguments
                case 'H' when _values.Count == 0:
                    _lineBufferLength = 0;
                    break;
                // H/f move cursor to line/column
                case 'H' when _values.Count == 2:
                case 'f' when _values.Count == 2:
                    _lineBufferLength = _values[1] - 1; // _values are line/column (1-based)
                    break;
                // move columns right
                case 'C' when _values.Count == 1:
                    _lineBufferLength += _values[0];
                    break;
                // move columns left
                case 'D' when _values.Count == 1:
                    _lineBufferLength = Math.Max(0, _lineBufferLength - _values[0]); // clamp to 0
                    break;
                // move to column
                case 'G' when _values.Count == 1:
                    _lineBufferLength = _values[0] - 1; // 1-based
                    break;
                default:
                    // skip, we aren't interested in these ansi codes
                    break;
            }
        }

        /// <summary>
        /// Performs a line break
        /// </summary>
        /// <param name="b">The character following the full line</param>
        private void doLineBreak(byte b)
        {
            if (char.IsWhiteSpace((char) b)) {
                // erase the space from the raw buffer and replace with \r\n
                _rawBuffer[_rawBufferLength - 1] = (byte) '\r';
                _rawBuffer[_rawBufferLength++] = (byte) '\n';
                // erase line build-up
                _lineBufferLength = 0;
            }
            else
            {
                // scan for the last space to know where to break the line
                var lastSpaceIndex = _lineBufferLength - 1;
                while ((lastSpaceIndex >= 0) && !char.IsWhiteSpace((char) _lineBuffer[lastSpaceIndex]))
                {
                    --lastSpaceIndex;
                }

                if (lastSpaceIndex < 0)
                {
                    // no spaces = one huge line (shouldn't really happen), just break it up by inserting \r\n
                    _rawBuffer[_rawBufferLength - 1] = (byte) '\r';
                    _rawBuffer[_rawBufferLength] = (byte) '\n';
                    _rawBuffer[_rawBufferLength + 1] = b;

                    _lineBuffer[0] = b;
                    _lineBufferToRawBuffer[0] = _rawBufferLength + 1;
                    _lineBufferLength = 1;

                    _rawBufferLength  += 2;
                }
                else
                {
                    // i is the location to place the \r\n
                    var insertIndex = _lineBufferToRawBuffer[lastSpaceIndex];
                    var moveLen = _rawBufferLength - insertIndex;
                    // shift stuff up to make room for \n
                    if (moveLen <= 0) {
                        throw new InvalidOperationException("Whoops");
                    }

                    Array.Copy(_rawBuffer, insertIndex, _rawBuffer, insertIndex + 1, moveLen);
                    // now insert, this destroys the space previously there and
                    // replaces with \r\n
                    _rawBuffer[insertIndex + 0] = (byte) '\r';
                    _rawBuffer[insertIndex + 1] = (byte) '\n';
                    ++_rawBufferLength;

                    // setup the new line buffer
                    var chars_to_adjust = _lineBufferLength - (lastSpaceIndex + 1);
                    int j, k;

                    Array.Copy(_lineBuffer, lastSpaceIndex + 1, _lineBuffer, 0, chars_to_adjust);

                    // adjust the raw pointers since we increased the pointer
                    // above
                    for (k = 0, j = (lastSpaceIndex + 1); j < _lineBufferLength; ++j, ++k)
                    {
                        // copy entire thing
                        _lineBufferToRawBuffer[k] = _lineBufferToRawBuffer[j];
                        // increase the raw pointer
                        ++_lineBufferToRawBuffer[k];
                    }
                    _lineBufferLength = chars_to_adjust;
                }
            }
        }

        private bool isAnsiSequenceFinished(char c) => char.IsLetter(c);
    }
}
