using MBBSEmu.CPU;
using NLog;
using System.Text;
using MBBSEmu.Memory;

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
            output.Append($"DX={cpu.Registers.DX:X4}  ");
            output.Append($"DS={cpu.Registers.DS:X4}  ");
            output.AppendLine($"ES={cpu.Registers.ES:X4}");
            output.Append($"SI={cpu.Registers.SI:X4}  ");
            output.Append($"DI={cpu.Registers.DI:X4}  ");
            output.Append($"SS={cpu.Registers.SS:X4}  ");
            output.Append($"IP={cpu.Registers.IP:X4}  ");
            output.Append($"SP={cpu.Registers.SP:X4}  ");
            output.AppendLine($"BP={cpu.Registers.BP:X4}");

            output.Append(cpu.Registers.F.IsFlagSet(EnumFlags.CF) ? "C" : "c");
            output.Append(cpu.Registers.F.IsFlagSet(EnumFlags.PF) ? "P" : "p");
            output.Append(cpu.Registers.F.IsFlagSet(EnumFlags.ZF) ? "Z" : "z");
            output.Append(cpu.Registers.F.IsFlagSet(EnumFlags.SF) ? "S" : "s");
            output.Append(cpu.Registers.F.IsFlagSet(EnumFlags.OF) ? "O" : "o");

            foreach (var line in output.ToString().Split("\r\n"))
            {
                l.Info(line);
            }
        }

        public static void InfoStack(this Logger l, CpuCore cpu, MemoryCore memory)
        {
            var output = new StringBuilder();
            l.Info("------------------------------------------");
            l.Info($"SP: {cpu.Registers.SP:X4}  BP: {cpu.Registers.BP:X4}");
            l.Info("------------------------------------------");
            for (var i = ushort.MaxValue; i >= cpu.Registers.SP; i-=2)
            {
                if (i == cpu.Registers.SP && i == cpu.Registers.BP)
                    output.Append("BP/SP-->");

                if (i != cpu.Registers.SP && i == cpu.Registers.BP)
                    output.Append("   BP-->");

                if (i == cpu.Registers.SP && i != cpu.Registers.BP)
                    output.Append("   SP-->");

                if (i != cpu.Registers.SP && i != cpu.Registers.BP)
                    output.Append("        ");

                output.Append(
                    $"{i:X4} [ {memory.GetWord(cpu.Registers.SS, (ushort) (i-1)):D5} 0x{memory.GetWord(cpu.Registers.SS, (ushort) (i-1)):X4} ] {i-1:X4}");

                l.Info(output);
                output.Clear();
            }
        }
    }
}
