using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MBBSEmu.CPU;
using MBBSEmu.Disassembler;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.Module;

namespace MBBSEmu
{
    class Program
    {
        static void Main(string[] args)
        {
            var sInputModule = string.Empty;
            var sInputPath = string.Empty;
            for (var i = 0; i < args.Length; i++)
            {
                

                switch (args[i].ToUpper())
                {
                    case "-M":
                        sInputModule = args[i + 1];
                        i++;
                        break;
                    case "-P":
                        sInputPath = args[i + 1];
                        i++;
                        break;
                    case "-?":
                        Console.WriteLine("-I <file> -- Input File to DisassembleSegment");
                        Console.WriteLine("-O <file> -- Output File for Disassembly (Default ConsoleUI)");
                        Console.WriteLine("-MINIMAL -- Minimal Disassembler Output");
                        Console.WriteLine(
                            "-ANALYSIS -- Additional Analysis on Imported Functions (if available)");
                        Console.WriteLine(
                            "-STRINGS -- Output all strings found in DATA segments at end of Disassembly");
                        return;
                    default:
                        Console.WriteLine($"Unknown Command Line Argument: {args[i]}");
                        return;
                }
            }

            if (string.IsNullOrEmpty(sInputModule))
            {
                Console.WriteLine("No Module Specified");
                return;
            }

            if (string.IsNullOrEmpty(sInputPath))
                sInputPath = Directory.GetCurrentDirectory();

            if (!sInputPath.EndsWith(@"\"))
                sInputPath += @"\";

            Console.WriteLine($"Loading Module {sInputModule}...");

            if (!File.Exists($"{sInputPath}{sInputModule}.MDF"))
            {
                Console.WriteLine($"Unable to locate Module: {sInputModule}");
                return;
            }

            Console.WriteLine($"Reading {sInputModule}.MDF...");
            var _mdf = new MdfFile($"{sInputPath}{sInputModule}.MDF");
            Console.WriteLine("-----------------");
            Console.WriteLine($"Module Name: {_mdf.ModuleName}");
            Console.WriteLine($"Developer: {_mdf.Developer}");
            Console.WriteLine($"DLLs: {string.Join(',',_mdf.DLLFiles)}");
            Console.WriteLine("-----------------");
            Console.WriteLine($"Loading {_mdf.DLLFiles[0]}.DLL...");
            var file = new NEFile($"{sInputPath}{_mdf.DLLFiles[0].Trim()}.DLL");

            Console.WriteLine($"Loaded {_mdf.DLLFiles[0]}.DLL ({file.FileContent.Length} bytes)");

            Console.WriteLine("Loading MSG File...");
            var msg = new MsgFile(sInputPath, sInputModule);

            Console.WriteLine("Initializing new X86_16 CPU...");
            var _cpu = new CpuCore();

            Console.WriteLine("Loading All Segments to Memory...");
            foreach (var seg in file.SegmentTable)
            {
                var segmentOffset = _cpu.Memory.AddSegment(seg);
                Console.WriteLine($"Segment {seg.Ordinal} ({seg.Data.Length} bytes) loaded at {segmentOffset}!");
            }

            Console.WriteLine("Locating _INIT_ function...");
            var initResidentName = file.ResidentNameTable.FirstOrDefault(x => x.Name.StartsWith("_INIT_"));

            if (initResidentName == null)
            {
                Console.WriteLine("Unable to locate _INIT_ entry in Resident Name Table");
                return;
            }

            Console.WriteLine($"Located Resident Name Table record for _INIT_: {initResidentName.Name}");
            Console.WriteLine($"Ordinal in Entry Table: {initResidentName.IndexIntoEntryTable}");
            var initEntryPoint = file.EntryTable
                .First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);
            Console.WriteLine(
                $"Located Entry Table Record, _INIT_ function located in Segment {initEntryPoint.SegmentNumber}");

            var initCode = file.SegmentTable.First(x => x.Ordinal == initEntryPoint.SegmentNumber);
            Console.WriteLine($"Starting Disassembly of Segment {initEntryPoint.SegmentNumber}");
            var dis = new ModuleDisassembly(initCode.Data);
            dis.Disassemble();
            Console.WriteLine($"Disassembly Complete! {dis.Instructions.Count} instructions disassembled");
            _cpu.Registers.CS = initCode.Ordinal;
            _cpu.Registers.IP = 0;
            Console.WriteLine($"Beginning Module Emulation...");
            Console.WriteLine($"Executing {initResidentName.Name} (Seg {initEntryPoint.SegmentNumber}:{initEntryPoint.Offset:X4}h)...");
            while (true)
            {
                _cpu.Tick();
                Console.ReadKey();
            }
        }
    }
}
