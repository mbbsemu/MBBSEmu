using MBBSEmu.CPU;
using MBBSEmu.Memory;
using NLog;
using System.Text;
using MBBSEmu.Extensions;

namespace MBBSEmu.Logging
{
    public static class LoggerExtension
    {
        /// <summary>
        ///     Takes a Byte Array and logs it in a hex-editor like format for easy reading
        /// </summary>
        /// <param name="l"></param>
        /// <param name="arrayToLog"></param>
        public static void InfoHex(this ILogger l, byte[] arrayToLog)
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

        public static void InfoRegisters(this ILogger l, CpuCore cpu)
        {
            var output = new StringBuilder();

            output.Append($"AX={cpu.Registers.AX:X4}  ");
            output.Append($"BX={cpu.Registers.BX:X4}  ");
            output.Append($"CX={cpu.Registers.CX:X4}  ");
            output.Append($"DX={cpu.Registers.DX:X4}  ");
            output.Append($"DS={cpu.Registers.DS:X4}  ");
            output.AppendLine($"ES={cpu.Registers.ES:X4}");
            output.Append($"SI={cpu.Registers.SI:X4}  ");
            output.Append($"DI={cpu.Registers.DI:X4}  ");
            output.Append($"SS={cpu.Registers.SS:X4}  ");
            output.Append($"IP={cpu.Registers.IP:X4}  ");
            output.Append($"SP={cpu.Registers.SP:X4}  ");
            output.AppendLine($"BP={cpu.Registers.BP:X4}");

            output.Append(cpu.Registers.CarryFlag ? "C" : "c");
            //output.Append(cpu.Registers.Parity ? "P" : "p");
            output.Append(cpu.Registers.ZeroFlag ? "Z" : "z");
            output.Append(cpu.Registers.SignFlag ? "S" : "s");
            output.Append(cpu.Registers.OverflowFlag ? "O" : "o");

            foreach (var line in output.ToString().Split("\r\n"))
            {
                l.Info(line);
            }
        }

        public static void InfoStack(this ILogger l, CpuRegisters registers, IMemoryCore memory)
        {
            var output = new StringBuilder();
            l.Info("------------------------------------------");
            l.Info($"SP: {registers.SP:X4}  BP: {registers.BP:X4}");
            l.Info("------------------------------------------");
            for (var i = ushort.MaxValue; i >= registers.SP; i-=2)
            {
                if (i == registers.SP && i == registers.BP)
                    output.Append("BP/SP-->");

                if (i != registers.SP && i == registers.BP)
                    output.Append("   BP-->");

                if (i == registers.SP && i != registers.BP)
                    output.Append("   SP-->");

                if (i != registers.SP && i != registers.BP)
                    output.Append("        ");

                output.Append(
                    $"{i:X4} [ {memory.GetWord(registers.SS, (ushort) (i-1)):D5} 0x{memory.GetWord(registers.SS, (ushort) (i-1)):X4} ] {i-1:X4}");

                l.Info(output);
                output.Clear();
            }
        }

        public static void InfoMemoryString(this ILogger l, IMemoryCore memory, ushort segment, ushort offset)
        {
            /*
             *            01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
             * _SEG:_OFF [ T  E  S  T        T  E  S  T     T  E  S  T]
             *
             */


            var sbOutput = new StringBuilder();
            sbOutput.Append("           ");
            //Print Header
            for (var i = 0; i < 0xF; i++)
            {
                sbOutput.Append($" {(byte)offset + i:X1}");
            }
            l.Info(sbOutput);
            sbOutput.Clear();

            sbOutput.Append($"{segment:X4}:{offset:X4} [");
            for (var i = 0; i < 0xF; i++)
            {
                sbOutput.Append($"  {(char)memory.GetByte(segment, (ushort) (offset+i))}");
            }

            sbOutput.Append("]");
            l.Info(sbOutput);
        }
    }
}
