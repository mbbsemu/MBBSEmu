using System;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public abstract class MajorbbsTestBase : TestBase
    {
        protected const ushort STACK_SEGMENT = 0;
        protected const ushort CODE_SEGMENT = 1;
        protected const ushort LIBRARY_SEGMENT = HostProcess.ExportedModules.Majorbbs.Segment;

        protected CpuCore mbbsEmuCpuCore;
        protected MemoryCore mbbsEmuMemoryCore;
        protected CpuRegisters mbbsEmuCpuRegisters;
        protected MbbsModule mbbsModule;
        protected HostProcess.ExportedModules.Majorbbs majorbbs;

        protected MajorbbsTestBase()
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsModule = new MbbsModule(FileUtility.CreateForTest(), null, string.Empty, mbbsEmuMemoryCore);
            majorbbs = new HostProcess.ExportedModules.Majorbbs(mbbsModule, new PointerDictionary<Session.SessionBase>());
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, MajorbbsFunctionDelegate);
        }

        private ReadOnlySpan<byte> MajorbbsFunctionDelegate(ushort ordinal, ushort functionOrdinal)
        {
            majorbbs.SetRegisters(mbbsEmuCpuRegisters);
            return majorbbs.Invoke(functionOrdinal, /* offsetsOnly= */ false);
        }

        protected void Reset()
        {
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = CODE_SEGMENT;
            mbbsEmuCpuRegisters.IP = 0;

            //Redeclare to re-allocate memory values that have been cleared
            majorbbs = new HostProcess.ExportedModules.Majorbbs(mbbsModule, new PointerDictionary<Session.SessionBase>());

        }

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort apiOrdinal, IEnumerable<ushort> apiArguments)
        {
            mbbsEmuMemoryCore.AddSegment(STACK_SEGMENT);

            //Create a new CODE Segment with a
            //simple ASM call for CALL FAR librarySegment:apiOrdinal
            var apiTestCodeSegment = new Segment
            {
                Ordinal = CODE_SEGMENT,
                Data = new byte[] { 0x9A, (byte)(apiOrdinal & 0xFF), (byte)(apiOrdinal >> 8), (byte)(LIBRARY_SEGMENT & 0xFF), (byte)(LIBRARY_SEGMENT >> 8), },
                Flag = (ushort)EnumSegmentFlags.Code
            };

            mbbsEmuMemoryCore.AddSegment(apiTestCodeSegment);

            //Push Arguments to Stack
            foreach (var a in apiArguments.Reverse())
                mbbsEmuCpuCore.Push(a);


            //Process Instruction, e.g. call the method
            mbbsEmuCpuCore.Tick();
        }

        /// <summary>
        ///     Executes an x86 Instruction to call the specified Library/API Ordinal with the specified arguments
        /// </summary>
        /// <param name="apiOrdinal"></param>
        /// <param name="apiArguments"></param>
        protected void ExecuteApiTest(ushort apiOrdinal, IEnumerable<IntPtr16> apiArguments)
        {
            var argumentsList = new List<ushort>(apiArguments.Count() * 2);

            foreach (var a in apiArguments)
            {
                argumentsList.Add(a.Offset);
                argumentsList.Add(a.Segment);
            }

            ExecuteApiTest(apiOrdinal, argumentsList);
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
