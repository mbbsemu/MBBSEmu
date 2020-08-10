using MBBSEmu.CPU;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace MBBSEmu.Tests.API
{
    public abstract class APITestBase
    {
        protected const ushort STACK_SEGMENT = 0;
        protected const ushort CODE_SEGMENT = 1;

        protected CpuCore mbbsEmuCpuCore;
        protected MemoryCore mbbsEmuMemoryCore;
        protected CpuRegisters mbbsEmuCpuRegisters;
        protected Majorbbs majorbbs;

        protected APITestBase()
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            majorbbs = new Majorbbs(new MbbsModule(null, string.Empty, mbbsEmuMemoryCore), new PointerDictionary<Session.SessionBase>());
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, majorbbsFunctionDelegate);
        }

        private ReadOnlySpan<byte> majorbbsFunctionDelegate(ushort ordinal, ushort functionOrdinal)
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
        }

        protected void executeAPITest(ushort librarySegment, ushort apiOrdinal, IEnumerable<ushort> apiArguments)
        {
            mbbsEmuMemoryCore.AddSegment(STACK_SEGMENT);

            //Create a new CODE Segment with a
            //simple ASM call for CALL FAR librarySegment:apiOrdinal
            var apiTestCodeSegment = new Segment
            {
                Ordinal = CODE_SEGMENT,
                Data = new byte[] { 0x9A, (byte)(apiOrdinal & 0xFF), (byte)(apiOrdinal >> 8), (byte)(librarySegment & 0xFF), (byte)(librarySegment >> 8), },
                Flag = (ushort)EnumSegmentFlags.Code
            };

            mbbsEmuMemoryCore.AddSegment(apiTestCodeSegment);

            //Push Arguments to Stack
            foreach (var a in apiArguments.Reverse())
                mbbsEmuCpuCore.Push(a);


            //Process Instruction, e.g. call the method
            mbbsEmuCpuCore.Tick();
        }

        protected void executeAPITest(ushort librarySegment, ushort apiOrdinal, IEnumerable<IntPtr16> apiArguments)
        {
            var argumentsList = new List<ushort>(apiArguments.Count() * 2);

            foreach (var a in apiArguments)
            {
                argumentsList.Add(a.Offset);
                argumentsList.Add(a.Segment);
            }

            executeAPITest(librarySegment, apiOrdinal, argumentsList);
        }
    }
}