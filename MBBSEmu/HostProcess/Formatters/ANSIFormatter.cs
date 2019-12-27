using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.HostProcess.Formatters
{
    /// <summary>
    ///     ANSI Formatting Class
    /// 
    ///     This applies ANSI formatting to our custom pipe delimited color codes
    /// </summary>
    public class ANSIFormatter : IANSIFormatter
    {
        private readonly Dictionary<string, string> _colorCodes;
        private readonly Dictionary<enmCursorDirection, string> _cursorCodes;

        /// <summary>
        ///     Color Enumerator (values match ElementAt() in ColorCodes)
        /// </summary>
        public enum enmColors : byte
        {
            Black = 10,
            Red = 12,
            Green = 14,
            Yellow = 16,
            Blue = 18,
            Magenta = 20,
            Cyan = 22,
            White = 24
        }

        /// <summary>
        ///     Cursor Direction Enumerator
        /// </summary>
        public enum enmCursorDirection : byte
        {
            Forward,
            Back,
            Up,
            Down
        }

        public ANSIFormatter()
        {
            _colorCodes = new Dictionary<string, string>
            {
                {"|RESET|", "\x1B[0m"},
                {"|B|", "\x1B[1m"},
                {"|!B|", "\x1B[22m"},
                {"|I|", "\x1B[3m"},
                {"|!I|", "\x1B[23m"},
                {"|U|", "\x1B[4m"},
                {"|!U|", "\x1B[24m"},
                {"|BLINK|", "\x1B[5m"},
                {"|!BLINK|", "\x1B[25m"},
                {"|BLINKFAST|", "\x1B[6m"},
                {"|BLACK|", "\x1B[30m"},
                {"|!BLACK|", "\x1B[40m"},
                {"|RED|", "\x1B[31m"},
                {"|!RED|", "\x1B[41m"},
                {"|GREEN", "\x1B[32m"},
                {"|!GREEN|", "\x1B[42m"},
                {"|YELLOW|", "\x1B[33m"},
                {"|!YELLOW|", "\x1B[43m"},
                {"|BLUE|", "\x1B[34m"},
                {"|!BLUE|", "\x1B[44m"},
                {"|MAGENTA|", "\x1B[35m"},
                {"|!MAGENTA|", "\x1B[45m"},
                {"|CYAN|", "\x1B[36m"},
                {"|!CYAN|", "\x1B[46m"},
                {"|WHITE|", "\x1B[37m"},
                {"|!WHITE|", "\x1B[47m"}
            };

            _cursorCodes = new Dictionary<enmCursorDirection, string>
            {
                {enmCursorDirection.Back, "\x1b[{0}D"},
                {enmCursorDirection.Forward, "\x1b[{0}C"},
                {enmCursorDirection.Up, "\x1b[{0}A"},
                {enmCursorDirection.Down, "\x1b[{0}B"}
            };

        }

        public string StringFormat(string input, params object[] options)
        {
            return string.Format(Encode(input), options);
        }

        public string Encode(string input)
        {
            return _colorCodes.Aggregate(input, (current, c) => current.Replace(c.Key, c.Value));
        }

        public string MoveCursor(enmCursorDirection direction, byte count = 1)
        {
            return string.Format(_cursorCodes[direction], count);
        }
    }
}
