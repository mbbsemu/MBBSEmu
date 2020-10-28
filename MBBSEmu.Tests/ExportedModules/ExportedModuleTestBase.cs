using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MBBSEmu.CPU;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;

namespace MBBSEmu.Tests.ExportedModules
{
    public abstract class ExportedModuleTestBase : TestBase
    {
        protected const ushort STACK_SEGMENT = 0;
        protected const ushort CODE_SEGMENT = 1;

        protected CpuCore mbbsEmuCpuCore;
        protected MemoryCore mbbsEmuMemoryCore;
        protected CpuRegisters mbbsEmuCpuRegisters;
        protected MbbsModule mbbsModule;
        protected HostProcess.ExportedModules.Majorbbs majorbbs;
        protected HostProcess.ExportedModules.Galgsbl galgsbl;
        protected PointerDictionary<SessionBase> testSessions;
        protected ServiceResolver _serviceResolver = new ServiceResolver();

        protected ExportedModuleTestBase() : this(Path.GetTempPath()) {}

        protected ExportedModuleTestBase(string modulePath)
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsModule = new MbbsModule(FileUtility.CreateForTest(), _serviceResolver.GetService<ILogger>(), null, modulePath, mbbsEmuMemoryCore);

            testSessions = new PointerDictionary<SessionBase>();
            testSessions.Allocate(new TestSession(null));
            testSessions.Allocate(new TestSession(null));

            majorbbs = new HostProcess.ExportedModules.Majorbbs(
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);

            galgsbl = new HostProcess.ExportedModules.Galgsbl(
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);

            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, ExportedFunctionDelegate);
        }

        private ReadOnlySpan<byte> ExportedFunctionDelegate(ushort ordinal, ushort functionOrdinal)
        {
            switch (ordinal)
            {
                case HostProcess.ExportedModules.Majorbbs.Segment:
                    {
                        majorbbs.SetRegisters(mbbsEmuCpuRegisters);
                        return majorbbs.Invoke(functionOrdinal, offsetsOnly: false);
                    }
                case HostProcess.ExportedModules.Galgsbl.Segment:
                    {
                        galgsbl.SetRegisters(mbbsEmuCpuRegisters);
                        return galgsbl.Invoke(functionOrdinal, offsetsOnly: false);
                    }
                default:
                    throw new Exception($"Unsupported Exported Module Segment: {ordinal}");
            }
        }

        protected void Reset()
        {
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = CODE_SEGMENT;
            mbbsEmuCpuRegisters.IP = 0;

            testSessions = new PointerDictionary<SessionBase>();
            testSessions.Allocate(new TestSession(null));
            testSessions.Allocate(new TestSession(null));

            //Redeclare to re-allocate memory values that have been cleared
            majorbbs = new HostProcess.ExportedModules.Majorbbs(
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);

            galgsbl = new HostProcess.ExportedModules.Galgsbl(
                _serviceResolver.GetService<ILogger>(),
                _serviceResolver.GetService<AppSettings>(),
                _serviceResolver.GetService<IFileUtility>(),
                _serviceResolver.GetService<IGlobalCache>(),
                mbbsModule,
                testSessions);
        }

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="exportedModuleSegment"></param>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort exportedModuleSegment, ushort apiOrdinal, IEnumerable<ushort> apiArguments)
        {
            if (!mbbsEmuMemoryCore.HasSegment(STACK_SEGMENT))
            {
                mbbsEmuMemoryCore.AddSegment(STACK_SEGMENT);
            }

            if (mbbsEmuMemoryCore.HasSegment(CODE_SEGMENT))
            {
                mbbsEmuMemoryCore.RemoveSegment(CODE_SEGMENT);
            }

            var apiTestCodeSegment = new Segment
            {
                Ordinal = CODE_SEGMENT,
                //Create a new CODE Segment with a
                //simple ASM call for CALL FAR librarySegment:apiOrdinal
                Data = new byte[] { 0x9A, (byte)(apiOrdinal & 0xFF), (byte)(apiOrdinal >> 8), (byte)(exportedModuleSegment & 0xFF), (byte)(exportedModuleSegment >> 8), },
                Flag = (ushort)EnumSegmentFlags.Code
            };
            mbbsEmuMemoryCore.AddSegment(apiTestCodeSegment);

            mbbsEmuCpuRegisters.CS = CODE_SEGMENT;
            mbbsEmuCpuRegisters.IP = 0;

            //Push Arguments to Stack
            foreach (var a in apiArguments.Reverse())
                mbbsEmuCpuCore.Push(a);

            //Process Instruction, e.g. call the method
            mbbsEmuCpuCore.Tick();

            foreach (var a in apiArguments)
                mbbsEmuCpuCore.Pop();
        }

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="exportedModuleSegment"></param>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort exportedModuleSegment, ushort apiOrdinal, IEnumerable<IntPtr16> apiArguments)
        {
            var argumentsList = new List<ushort>(apiArguments.Count() * 2);

            foreach (var a in apiArguments)
            {
                argumentsList.Add(a.Offset);
                argumentsList.Add(a.Segment);
            }

            ExecuteApiTest(exportedModuleSegment, apiOrdinal, argumentsList);
        }

        /// <summary>
        ///     Executes a test directly against the MajorBBS Exported Module to evaluate the return value of a given property
        ///
        ///     We invoke these directly as properties are handled at decompile time by applying the relocation information to the memory
        ///     address for the property. Because Unit Tests aren't going through the same relocation process, we simulate it by getting the
        ///     SEG:OFF of the Property as it would be returned during relocation. This allows us to evaluate the given value of the returned
        ///     address.
        /// </summary>
        /// <param name="apiOrdinal"></param>
        protected ReadOnlySpan<byte> ExecutePropertyTest(ushort apiOrdinal) => majorbbs.Invoke(apiOrdinal);
    }
}
