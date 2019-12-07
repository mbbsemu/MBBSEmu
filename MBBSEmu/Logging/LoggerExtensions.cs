using System.IO;
using MBBSEmu.CPU;
using NLog;
using System.Text;
using SQLitePCL;

namespace MBBSEmu.Logging
{
    public static class LoggerExtension
    {
        /// <summary>
        ///     Takes a Byte Array and logs it in a hex-editor like format for easy reading
        /// </summary>
        /// <param name="l"></param>
        /// <param name="arrayToLog"></param>
        public static void InfoHex(this Logger l, byte[] arrayToLog)
        {
            var output = new StringBuilder();

            //Print Header
            output.AppendLine(new string('-', 73));
            output.AppendLine($"{arrayToLog.Length} bytes, 0x0000 -> 0x{arrayToLog.GetUpperBound(0):X4}");
            output.AppendLine(new string('-', 73));
            output.Append("      ");
            for (var i = 0; i < 0x10; i++)
            {
                output.Append($" {i:X2}");
            }
            output.AppendLine();
            var hexString = new StringBuilder(47);
            var literalString = new StringBuilder(15);

            //Print Hex Values
            for (var i = 0; i < arrayToLog.Length; i++)
            {
                hexString.Append($" {arrayToLog[i]:X2}");
                literalString.Append(arrayToLog[i] < 32 ? ' ' : (char)arrayToLog[i]);

                //New Memory Page
                if ((i | 0x0F) == i)
                {
                    output.AppendLine($"{(i & ~0xF):X4} [{hexString} ] {literalString}");
                    hexString.Clear();
                    literalString.Clear();
                }
            }

            //Flush any data remaining in the buffer
            if (hexString.Length > 0)
            {
                output.AppendLine($"{(arrayToLog.Length & ~0xF):X4} [{hexString.ToString().PadRight(48)} ] {literalString}");
                hexString.Clear();
                literalString.Clear();
            }

            l.Info($"\r\n{output}");
        }

        public static void InfoRegisters(this Logger l, CpuCore cpu)
        {
            var output = new StringBuilder();

            output.Append($"AX={cpu.Registers.AX:X4}  ");
            output.Append($"BX={cpu.Registers.BX:X4}  ");
            output.Append($"CX={cpu.Registers.CX:X4}  ");
            output.Append($"DX={cpu.Registers.CX:X4}  ");
            output.Append($"DS={cpu.Registers.DS:X4}  ");
            output.AppendLine($"ES={cpu.Registers.ES:X4}"); 
            output.Append($"SS={cpu.Registers.SS:X4}  ");
            output.Append($"IP={cpu.Registers.IP:X4}  ");
            output.Append($"SP={cpu.Registers.SP:X4}  ");
            output.AppendLine($"BP={cpu.Registers.BP:X4}");

            foreach (var line in output.ToString().Split("\r\n"))
            {
                l.Info(line);
            }
        }

        public static void InfoStack(this Logger l, CpuCore cpu)
        {
            var output = new StringBuilder();
            for (ushort i = ushort.MaxValue - 2; i >= cpu.Registers.SP; i-=2)
            {
                output.Append(
                    $"{i:X4} [ {cpu.Memory.GetWord(cpu.Registers.SS, i):D5} 0x{cpu.Memory.GetWord(cpu.Registers.SS, i):X4} ]");

                if (i == cpu.Registers.SP)
                    output.Append(" <-- SP ");

                output.AppendLine(string.Empty);
            }

            foreach (var line in output.ToString().Split("\r\n"))
            {
                l.Info(line);
            }
        }
    }
}
