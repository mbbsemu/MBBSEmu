using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using Microsoft.VisualBasic.FileIO;
using System;

namespace MBBSEmu.Tests.API
{
    public abstract class APITestBase
    {
        protected const ushort STACK_SEGMENT = 0;
        protected const ushort CODE_SEGMENT = 1;
        protected const ushort DATA_SEGMENT = 2;

        protected CpuCore mbbsEmuCpuCore;
        protected MemoryCore mbbsEmuMemoryCore;
        protected CpuRegisters mbbsEmuCpuRegisters;
        protected Majorbbs majorbbs;

        protected APITestBase()
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            majorbbs = new Majorbbs(new MbbsModule(null, "", mbbsEmuMemoryCore), new PointerDictionary<Session.SessionBase>());
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

        protected void CreateCodeSegment(ReadOnlySpan<byte> byteCode, ushort segmentOrdinal = 1)
        {
            //Decode the Segment
            var instructionList = new InstructionList();
            var codeReader = new ByteArrayCodeReader(byteCode.ToArray());
            var decoder = Decoder.Create(16, codeReader);
            decoder.IP = 0x0;

            while (decoder.IP < (ulong)byteCode.Length)
            {
                decoder.Decode(out instructionList.AllocUninitializedElement());
            }

            mbbsEmuMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        
        /// <summary>
        /// Allows the test method to push any method arguments into core
        /// </summary>
        /// <param name="core">CPU Core where arguments should be pushed</param>
        /// <returns>The data to be copied into the data segment, or null for none</returns>
        protected delegate byte[] ArgumentPusher(ICpuCore core);
        
        protected void executeAPITest(ushort ordinal, ArgumentPusher argumentPusher)
        {
            Reset();

            mbbsEmuMemoryCore.AddSegment(STACK_SEGMENT);
            mbbsEmuMemoryCore.AddSegment(DATA_SEGMENT);
            
            CreateCodeSegment(new byte[] { 0x9A, (byte) (ordinal & 0xFF), (byte) (ordinal >> 8), 0xFF, 0xFF });

            byte[] dataSegmentData = argumentPusher.Invoke(mbbsEmuCpuCore);
            if (dataSegmentData != null)
            {
                mbbsEmuMemoryCore.SetArray(DATA_SEGMENT, 0, dataSegmentData);
            }

            //Process Instruction, e.g. call the method
            mbbsEmuCpuCore.Tick();
        }
    }
}