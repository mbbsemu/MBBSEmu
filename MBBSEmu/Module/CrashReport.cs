using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.Resources;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Class to Generate a Crash Report for a Module
    /// </summary>
    public class CrashReport
    {
        private MbbsModule _moduleToReport;
        private ICpuRegisters _registers;
        private Exception _exception;

        public CrashReport()
        {
        }

        public CrashReport(MbbsModule moduleToReport, ICpuRegisters registers, Exception exception)
        {
            _moduleToReport = moduleToReport;
            _registers = registers;
            _exception = exception;

        }

        public void Save(MbbsModule moduleToReport, ICpuRegisters registers, Exception exception)
        {
            _moduleToReport = moduleToReport;
            _registers = registers;
            _exception = exception;

            Save();
        }

        public void Save(string fileName = "")
        {
            var crashReportVariables = new List<string>();
            crashReportVariables.Add(new ResourceManager().GetString("MBBSEmu.Assets.version.txt"));
            //Built Variable List to be used in Crash Report Template (crashReportTemplate.txt)
            crashReportVariables.Add(DateTime.Now.ToString("d"));
            crashReportVariables.Add(DateTime.Now.ToString("t"));

            //Current Operating System
            crashReportVariables.Add(RuntimeInformation.OSDescription);
            crashReportVariables.Add(RuntimeInformation.OSArchitecture.ToString());

            //Module Information
            crashReportVariables.Add(_moduleToReport.ModuleIdentifier);
            crashReportVariables.Add(_moduleToReport.ModulePath);
            crashReportVariables.Add(_moduleToReport.MainModuleDll.File.FileName);
            crashReportVariables.Add(_moduleToReport.MainModuleDll.File.FileContent.Length.ToString());
            crashReportVariables.Add(_moduleToReport.MainModuleDll.File.CRC32);

            //Exception Information
            crashReportVariables.Add(_exception.Message);
            crashReportVariables.Add(_exception.StackTrace);

            //CPU Instruction
            crashReportVariables.Add(_moduleToReport.Memory.GetInstruction(_registers.CS, _registers.IP).ToString());

            //Registers
            crashReportVariables.Add(_registers.ToString());
            crashReportVariables.Add(_moduleToReport.Memory.GetMemorySegment(0).Slice(_registers.BP, (_registers.BP - _registers.SP))
                .ToHexString(_registers.BP, (ushort)(_registers.BP - _registers.SP)));

            var crashTemplate = new ResourceManager().GetString("MBBSEmu.Assets.crashReportTemplate.txt");

            //Replace Variables in Template
            var crashReport = string.Format(crashTemplate, crashReportVariables.ToArray());

            if(string.IsNullOrEmpty(fileName))
                fileName = $"Crash_{_moduleToReport.ModuleIdentifier}_{DateTime.Now:yyyyMMddHHmmss}.txt";

            //Write Crash Report to File Named with the Module Identifier and the current time
            System.IO.File.WriteAllText(fileName, crashReport);

        }
    }
}

