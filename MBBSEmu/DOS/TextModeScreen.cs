using MBBSEmu.IO;
using MBBSEmu.Memory;
using System;
using System.Text;

namespace MBBSEmu.DOS
{
    /// <summary>
    ///     80x25 color text mode screen at B800:0000
    ///
    ///     Programs using direct video (Borland conio, etc.) write character/attribute pairs straight
    ///     into video memory instead of calling DOS or the BIOS to print. Flush() compares video memory
    ///     to what was last sent and writes the changed cells to stdout as ANSI, so those programs are
    ///     visible on a terminal. Programs that never touch video memory produce no output from here.
    /// </summary>
    public class TextModeScreen
    {
        public const ushort VIDEO_SEGMENT = 0xB800;
        public const int COLUMNS = 80;
        public const int ROWS = 25;
        private const int CELLS = COLUMNS * ROWS;

        //CGA color order (blue, green, cyan, red, ...) to ANSI color order (red, green, yellow, blue, ...)
        private static readonly int[] CgaToAnsi = { 0, 4, 2, 6, 1, 5, 3, 7 };

        private readonly IMemoryCore _memory;
        private readonly IStream _stdout;
        private readonly byte[] _shadow = new byte[CELLS * 2];
        private bool _started;

        public byte CursorX { get; set; }
        public byte CursorY { get; set; }

        public TextModeScreen(IMemoryCore memory, IStream stdout)
        {
            _memory = memory;
            _stdout = stdout;
        }

        /// <summary>
        ///     Sends any cells changed since the last flush, then places the cursor
        /// </summary>
        public void Flush()
        {
            var video = _memory.GetArray(VIDEO_SEGMENT, 0, CELLS * 2);
            if (video.SequenceEqual(_shadow))
                return;

            var output = new StringBuilder();
            if (!_started)
            {
                output.Append("\x1B[0m\x1B[2J");
                _started = true;
            }

            var lastAttribute = -1;
            var nextCell = -1;
            for (var cell = 0; cell < CELLS; cell++)
            {
                var character = video[cell * 2];
                var attribute = video[cell * 2 + 1];
                if (character == _shadow[cell * 2] && attribute == _shadow[cell * 2 + 1])
                    continue;

                //The last column isn't written, so the terminal never scrolls or wraps
                if (cell % COLUMNS == COLUMNS - 1 && cell / COLUMNS == ROWS - 1)
                    continue;

                //Position at each row start too, rather than relying on how the terminal wraps after column 80
                if (cell != nextCell || cell % COLUMNS == 0)
                    output.Append($"\x1B[{cell / COLUMNS + 1};{cell % COLUMNS + 1}H");

                if (attribute != lastAttribute)
                {
                    output.Append(AnsiAttribute(attribute));
                    lastAttribute = attribute;
                }

                //Control codes would move the terminal cursor; the screen shows them as glyphs we can't map, so use a space
                output.Append(character < 0x20 || character == 0x7F ? ' ' : (char)character);
                nextCell = cell + 1;
            }

            video.CopyTo(_shadow);

            output.Append("\x1B[0m");
            output.Append($"\x1B[{CursorY + 1};{CursorX + 1}H");

            //Characters above 0x7F are CP437 bytes; the session maps them to Unicode
            foreach (var c in output.ToString())
                _stdout.Write((byte)c);
            _stdout.Flush();
        }

        private static string AnsiAttribute(byte attribute)
        {
            var foreground = CgaToAnsi[attribute & 0x07];
            var background = CgaToAnsi[(attribute >> 4) & 0x07];
            var bold = (attribute & 0x08) != 0 ? "1" : "22";
            return $"\x1B[{bold};{30 + foreground};{40 + background}m";
        }
    }

    /// <summary>
    ///     Console stream for DOS output that first sends any pending video memory changes, so text
    ///     written through DOS and text drawn into video memory reach the terminal in program order
    /// </summary>
    public class TextModeScreenOrderedStream : IStream
    {
        private readonly IStream _stream;
        private readonly TextModeScreen _screen;

        public TextModeScreenOrderedStream(IStream stream, TextModeScreen screen)
        {
            _stream = stream;
            _screen = screen;
        }

        public int ReadByte() => _stream.ReadByte();

        public void Write(byte c)
        {
            _screen.Flush();
            _stream.Write(c);
        }

        public void Write(byte[] c)
        {
            _screen.Flush();
            _stream.Write(c);
        }

        public void Flush() => _stream.Flush();

        public void Dispose() => _stream.Dispose();
    }
}
