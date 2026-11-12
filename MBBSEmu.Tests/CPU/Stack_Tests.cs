using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Memory;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class Stack_Tests : CpuTestBase
    {
        [Fact]
        public void Push_Pop_Single_Value_Byte()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0); //Stack Segment
            mbbsEmuCpuCore.Push(byte.MaxValue);
            Assert.Equal(byte.MaxValue, mbbsEmuCpuCore.Pop());
        }

        [Fact]
        public void Push_Pop_Single_Value_Ushort()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0); //Stack Segment
            mbbsEmuCpuCore.Push(ushort.MaxValue);
            mbbsEmuCpuCore.Push(ushort.MaxValue - 1);
            Assert.Equal(ushort.MaxValue - 1, mbbsEmuCpuCore.Pop());
            Assert.Equal(ushort.MaxValue, mbbsEmuCpuCore.Pop());
        }

        [Fact]
        public void Push_Sp_Pushes_Original_Sp_And_Pop_Sp_Restores_Value()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.SP = 0x1000;

            var instructions = new Assembler(16);
            instructions.push(sp);
            instructions.pop(ax);
            instructions.push(0x2000);
            instructions.pop(sp);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick(); // push sp
            Assert.Equal(0x0FFE, mbbsEmuCpuRegisters.SP);

            mbbsEmuCpuCore.Tick(); // pop ax
            Assert.Equal(0x1000, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0x1000, mbbsEmuCpuRegisters.SP);

            mbbsEmuCpuCore.Tick(); // push 0x2000
            mbbsEmuCpuCore.Tick(); // pop sp
            Assert.Equal(0x2000, mbbsEmuCpuRegisters.SP);
        }

        [Fact]
        public void CallFar_Updates_Registers_CS_For_Mov_Ax_Cs()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);

            var caller = new Assembler(16);
            caller.call(2, 0);
            caller.hlt();
            CreateCodeSegment(caller, 1);

            var callee = new Assembler(16);
            callee.mov(ax, cs);
            callee.retf();
            CreateCodeSegment(callee, 2);

            mbbsEmuCpuCore.Run();
            Assert.Equal(2, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void ShortSegment_OutOfBounds_Access_Throws()
        {
            Reset();
            var shortSeg = mbbsEmuMemoryCore.AllocateRealModeSegment(16);
            mbbsEmuCpuRegisters.DS = shortSeg.Segment;

            var instructions = new Assembler(16);
            instructions.mov(al, __byte_ptr[32]);
            CreateCodeSegment(instructions);

            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Tick());
        }

        [Fact]
        public void InstructionList_Only_Segment_Executes_Via_Fallback()
        {
            Reset();
            var instructions = new Assembler(16);
            instructions.mov(ax, 0x1234);
            instructions.hlt();

            var stream = new System.IO.MemoryStream();
            instructions.Assemble(new StreamCodeWriter(stream), 0);
            var byteCode = stream.ToArray();

            var instructionList = new InstructionList();
            var decoder = Decoder.Create(16, new ByteArrayCodeReader(byteCode));
            while (decoder.IP < (ulong)byteCode.Length)
                decoder.Decode(out instructionList.AllocUninitializedElement());

            CreateCodeSegment(instructionList, 1);

            mbbsEmuCpuCore.Run();
            Assert.Equal(0x1234, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void LazyRecompile_LongInstruction_Succeeds()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;

            // 9-byte instruction: mov dword ptr [0], 0x12345678 (66 C7 06 00 00 78 56 34 12) + HLT (F4)
            byte[] code = [0x66, 0xC7, 0x06, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12, 0xF4];
            mbbsEmuMemoryCore.SetArray(1, 0, code);

            mbbsEmuCpuCore.Run();
            Assert.Equal(0x12345678u, mbbsEmuMemoryCore.GetDWord(2, 0));
        }

        [Fact]
        public void Mov_UnsupportedSegmentRegister_Routes_To_Fallback_And_Throws()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            // MOV AX, FS (8C E0)
            mbbsEmuMemoryCore.SetArray(1, 0, [0x8C, 0xE0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Tick());

            mbbsEmuCpuRegisters.IP = 0;
            // MOV GS, AX (8E E8)
            mbbsEmuMemoryCore.SetArray(1, 0, [0x8E, 0xE8]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Tick());
        }

        [Fact]
        public void FastPath_Exception_During_Run_Synchronizes_Registers_And_Faulting_IP()
        {
            Reset();
            var instructions = new Assembler(16);
            // 3 bytes: MOV AX, 0x1234 (B8 34 12)
            instructions.mov(ax, 0x1234);
            // Faulting instruction at IP = 3: MOV BX, ES:[0] (26 8B 1E 00 00) where ES = 0xFFFF (unmapped)
            instructions.mov(bx, __word_ptr.es[0]);
            instructions.hlt();
            CreateCodeSegment(instructions);

            Assert.Throws<NullReferenceException>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x1234, mbbsEmuCpuRegisters.AX);
            Assert.Equal(3, mbbsEmuCpuRegisters.IP);
        }

        [Fact]
        public void AddSegment_ReusedOrdinal_Clears_Previous_Bytes_Beyond_New_Data()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetByte(2, 32, 0x7F);

            var segment = new Disassembler.Artifacts.Segment
            {
                Ordinal = 2,
                Data = [0xAA, 0xBB],
                Flag = (ushort)Disassembler.Artifacts.EnumSegmentFlags.Data
            };
            mbbsEmuProtectedModeMemoryCore.AddSegment(segment);

            Assert.Equal(0xAA, mbbsEmuMemoryCore.GetByte(2, 0));
            Assert.Equal(0xBB, mbbsEmuMemoryCore.GetByte(2, 1));
            Assert.Equal(0x00, mbbsEmuMemoryCore.GetByte(2, 32));
        }

        [Fact]
        public void RealMode_BoundaryStraddlingInstruction_ReadsLinearBytes_AndTruncatesIP()
        {
            var realModeMemory = new MBBSEmu.Memory.RealModeMemoryCore(logger: null);
            using var cpu = new MBBSEmu.CPU.CpuCore(logger: null);
            cpu.Reset(realModeMemory, null, null, null);
            cpu.CS = 0x1000;
            cpu.IP = 0xFFFF;

            // Populate 1000:0000..0001 with 0x99, 0x99 and 1000:0002 with HLT (0xF4)
            realModeMemory.SetByte(0x1000, 0x0000, 0x99);
            realModeMemory.SetByte(0x1000, 0x0001, 0x99);
            realModeMemory.SetByte(0x1000, 0x0002, 0xF4);

            // Place MOV AX, 0x1234 (B8 34 12) straddling 1000:FFFF into the following linear bytes (2000:0000..0001)
            realModeMemory.SetByte(0x1000, 0xFFFF, 0xB8);
            realModeMemory.SetByte(0x2000, 0x0000, 0x34);
            realModeMemory.SetByte(0x2000, 0x0001, 0x12);

            cpu.Run();

            Assert.Equal(0x1234, cpu.AX);
            Assert.Equal(0x0003, cpu.IP);
        }

        [Fact]
        public void RepMovsb_And_RepMovsw_ForwardNonOverlapping_And_Overlapping()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;

            // 1. Forward non-overlapping REP MOVSB (bulk-copy path)
            for (ushort i = 0; i < 64; i++)
                mbbsEmuMemoryCore.SetByte(2, i, (byte)(i + 1));

            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.DI = 0x100;
            mbbsEmuCpuRegisters.CX = 64;
            mbbsEmuCpuRegisters.DirectionFlag = false;

            var instructions = new Assembler(16);
            instructions.rep.movsb();
            // 2. Forward overlapping REP MOVSB (DI = SI + 1, scalar propagation path)
            instructions.mov(si, 0x200);
            instructions.mov(di, 0x201);
            instructions.mov(cx, 15);
            instructions.rep.movsb();
            // 3. Forward overlapping REP MOVSW (DI = SI + 2, scalar propagation path)
            instructions.mov(si, 0x300);
            instructions.mov(di, 0x302);
            instructions.mov(cx, 7);
            instructions.rep.movsw();
            instructions.hlt();
            CreateCodeSegment(instructions);

            mbbsEmuMemoryCore.SetByte(2, 0x200, 0x5A);
            mbbsEmuMemoryCore.SetWord(2, 0x300, 0xCAFE);

            mbbsEmuCpuCore.Run();

            for (ushort i = 0; i < 64; i++)
                Assert.Equal((byte)(i + 1), mbbsEmuMemoryCore.GetByte(2, (ushort)(0x100 + i)));

            for (ushort i = 0; i < 16; i++)
                Assert.Equal(0x5A, mbbsEmuMemoryCore.GetByte(2, (ushort)(0x200 + i)));

            for (ushort i = 0; i < 8; i++)
                Assert.Equal(0xCAFE, mbbsEmuMemoryCore.GetWord(2, (ushort)(0x300 + i * 2)));
        }

        [Fact]
        public void InstructionCounter_BalancedOnFastPathFault_And_NonHaltingFallback()
        {
            // 1. Non-halting fallback (NOP -> CWD -> HLT) should report 3 instructions
            Reset();
            var instructions = new Assembler(16);
            instructions.nop();
            instructions.cwd();
            instructions.hlt();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.InstructionCounter = 0;
            mbbsEmuCpuCore.Run();
            Assert.Equal(3L, mbbsEmuCpuCore.InstructionCounter);

            // 2. Fast-path memory fault after 1st MOV should leave InstructionCounter at 2 (attempted instructions)
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            Reset();
            mbbsEmuCpuRegisters.DS = 99; // unmapped segment
            var faultInstructions = new Assembler(16);
            faultInstructions.mov(ax, 0x1234);
            faultInstructions.mov(bx, __word_ptr[0x0000]);
            faultInstructions.hlt();
            CreateCodeSegment(faultInstructions);

            mbbsEmuCpuCore.InstructionCounter = 0;
            Assert.ThrowsAny<Exception>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x1234, mbbsEmuCpuRegisters.AX);
            Assert.Equal(2L, mbbsEmuCpuCore.InstructionCounter);
        }

        [Fact]
        public void RealMode_Les_AtSegmentBoundary_ReadsLinearBytes_And_ShortSegmentFaultsBeforeModifyingRegister()
        {
            var realModeMemory = new MBBSEmu.Memory.RealModeMemoryCore(logger: null);
            using var cpu = new MBBSEmu.CPU.CpuCore(logger: null);
            cpu.Reset(realModeMemory, null, null, null);
            cpu.CS = 0x1000;
            cpu.DS = 0x2000;
            cpu.IP = 0;

            // Populate 2000:0000..0001 with 0x9999 so a wrapped read would load ES=0x9999
            realModeMemory.SetWord(0x2000, 0x0000, 0x9999);
            // Populate 2000:FFFE..FFFF with offset 0x1122 and following linear bytes (3000:0000..0001) with segment 0x3344
            realModeMemory.SetWord(0x2000, 0xFFFE, 0x1122);
            realModeMemory.SetWord(0x3000, 0x0000, 0x3344);

            // LES BX, [0xFFFE] (C4 1E FE FF), HLT (F4)
            realModeMemory.SetArray(0x1000, 0, new byte[] { 0xC4, 0x1E, 0xFE, 0xFF, 0xF4 });

            cpu.Run();

            Assert.Equal(0x1122, cpu.BX);
            Assert.Equal(0x3344, cpu.ES);

            // In protected mode on a short segment, LES past the end faults without modifying BX
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2, size: 16);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.BX = 0x7777;
            mbbsEmuMemoryCore.SetWord(2, 14, 0x1122);

            var lesInstructions = new Assembler(16);
            lesInstructions.les(bx, __dword_ptr[14]);
            lesInstructions.hlt();
            CreateCodeSegment(lesInstructions);

            Assert.ThrowsAny<Exception>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x7777, mbbsEmuCpuRegisters.BX);
        }

        [Fact]
        public void ExcessivePrefixes_Exceeding15Bytes_RoutesToFallbackAndFaults()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            // 14 ES override prefixes (0x26) + MOV AX, 0x1234 (B8 34 12) = 17 bytes (> 15-byte x86 max)
            var bytes = new byte[18];
            for (var i = 0; i < 14; i++)
                bytes[i] = 0x26;
            bytes[14] = 0xB8;
            bytes[15] = 0x34;
            bytes[16] = 0x12;
            bytes[17] = 0xF4;
            mbbsEmuMemoryCore.SetArray(1, 0, bytes);

            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuCpuRegisters.AX = 0x5555;

            Assert.ThrowsAny<Exception>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x5555, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void ProtectedMode_WordAccessAt0xFFFF_FaultsInsteadOfAccessingGuardBytes()
        {
            // 1. MOV AX, [0xFFFF] (A1 FF FF)
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xA1, 0xFF, 0xFF, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 2. MOV [0xFFFF], AX (A3 FF FF)
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xA3, 0xFF, 0xFF, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 3. MOV BX, [SI] with SI=0xFFFF (8B 1C)
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuCpuRegisters.SI = 0xFFFF;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0x8B, 0x1C, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 4. STOSW with DI=0xFFFF (AB)
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 0xFFFF;
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xAB, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());
        }

        [Fact]
        public void ProtectedMode_InstructionStraddling0xFFFF_FaultsInsteadOfReadingGuardBytes()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            // 1-byte HLT at 0xFFFF is valid in a 64 KiB segment
            mbbsEmuMemoryCore.SetByte(1, 0xFFFF, 0xF4);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.IP = 0xFFFF;
            mbbsEmuCpuCore.Run();
            Assert.True(mbbsEmuCpuRegisters.Halt);

            // Multi-byte MOV AX, imm16 starting at 0xFFFF straddles segment end and must fault
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetByte(2, 0xFFFF, 0xB8);
            mbbsEmuCpuRegisters.Halt = false;
            mbbsEmuCpuRegisters.AX = 0x5555;
            mbbsEmuCpuRegisters.CS = 2;
            mbbsEmuCpuRegisters.IP = 0xFFFF;
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x5555, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void InvalidModRmEncodings_MovImm_And_LeaRegister_Fault()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuCpuRegisters.CS = 1;

            // 0xC6 /1 (C6 C8 12) is invalid (only /0 is MOV r/m8, imm8)
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xC6, 0xC8, 0x12, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 0xC7 /1 (C7 C8 34 12) is invalid (only /0 is MOV r/m16, imm16)
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xC7, 0xC8, 0x34, 0x12, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 0x8D with mod=11 register operand (8D C0) is invalid for LEA
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuCpuRegisters.AX = 0x1111;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0x8D, 0xC0, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());
            Assert.Equal(0x1111, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void RepneStosb_RespectsZeroFlagTermination()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.AL = 0x7F;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuCpuRegisters.CX = 4;
            mbbsEmuCpuRegisters.ZeroFlag = true;
            mbbsEmuCpuRegisters.DirectionFlag = false;

            // F2 AA (REPNE STOSB), F4 (HLT)
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xF2, 0xAA, 0xF4 });
            mbbsEmuCpuCore.Run();

            Assert.Equal(3, mbbsEmuCpuRegisters.CX);
            Assert.Equal(1, mbbsEmuCpuRegisters.DI);
            Assert.Equal(0x7F, mbbsEmuMemoryCore.GetByte(2, 0));
            Assert.Equal(0x00, mbbsEmuMemoryCore.GetByte(2, 1));
        }

        [Fact]
        public void Pusha_Popa_RoundTripsRegisters_InFastAndFallbackPaths()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.AX = 0x1111;
            mbbsEmuCpuRegisters.CX = 0x2222;
            mbbsEmuCpuRegisters.DX = 0x3333;
            mbbsEmuCpuRegisters.BX = 0x4444;
            mbbsEmuCpuRegisters.SP = 0x1000;
            mbbsEmuCpuRegisters.BP = 0x5555;
            mbbsEmuCpuRegisters.SI = 0x6666;
            mbbsEmuCpuRegisters.DI = 0x7777;

            // 60 (PUSHA), B8..BF (zero regs), 61 (POPA), F4 (HLT)
            // Place at 0xFFF1 so it also exercises TickFallback() in protected mode
            mbbsEmuMemoryCore.SetArray(1, 0xFFF1, new byte[]
            {
                0x60,
                0x31, 0xC0, // XOR AX, AX
                0x31, 0xC9, // XOR CX, CX
                0x31, 0xD2, // XOR DX, DX
                0x31, 0xDB, // XOR BX, BX
                0x61,
                0xF4
            });
            mbbsEmuCpuRegisters.IP = 0xFFF1;
            mbbsEmuCpuCore.Run();

            Assert.Equal(0x1111, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0x2222, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0x3333, mbbsEmuCpuRegisters.DX);
            Assert.Equal(0x4444, mbbsEmuCpuRegisters.BX);
            Assert.Equal(0x1000, mbbsEmuCpuRegisters.SP);
            Assert.Equal(0x5555, mbbsEmuCpuRegisters.BP);
            Assert.Equal(0x6666, mbbsEmuCpuRegisters.SI);
            Assert.Equal(0x7777, mbbsEmuCpuRegisters.DI);
        }

        [Fact]
        public void RealMode_WordAccessAt0xFFFF_WithNonZeroSegment_AccessesCorrectSegment()
        {
            var realMemory = new RealModeMemoryCore(logger: null);
            using var cpu = new CpuCore(logger: null);
            cpu.Reset(realMemory, null, null, null);
            cpu.Registers.CS = 0x3000;
            cpu.Registers.DS = 0x2000;
            cpu.Registers.IP = 0;
            cpu.Registers.AX = 0xBEEF;

            // A3 FF FF (MOV [0xFFFF], AX), B8 00 00 (MOV AX, 0), A1 FF FF (MOV AX, [0xFFFF]), F4 (HLT)
            realMemory.SetArray(0x3000, 0, new byte[]
            {
                0xA3, 0xFF, 0xFF,
                0xB8, 0x00, 0x00,
                0xA1, 0xFF, 0xFF,
                0xF4
            });

            cpu.Run();

            Assert.Equal(0xBEEF, cpu.Registers.AX);
            Assert.Equal(0xBEEF, realMemory.GetWord(0x2000, 0xFFFF));
            Assert.Equal(0x0000, realMemory.GetWord(0x0000, 0xFFFF));
        }

        [Fact]
        public void ProtectedMode_GetMemorySegment_ReturnsLogicalLength()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            Assert.Equal(0x10000, mbbsEmuProtectedModeMemoryCore.GetMemorySegment(1).Length);
        }

        [Fact]
        public void InvalidGroup2SubOp6_And_MovCsRm16_FaultInFastPath()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuCpuRegisters.CS = 1;

            // C1 F0 00: Group 2 /6 with imm8 == 0 must fault instead of acting as a no-op
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0xC1, 0xF0, 0x00, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());

            // 8E C8: MOV CS, AX (/1) is invalid on 286+ and must fault in fast path
            mbbsEmuCpuRegisters.Halt = false;
            mbbsEmuCpuRegisters.IP = 0x10;
            mbbsEmuMemoryCore.SetArray(1, 0x10, new byte[] { 0x8E, 0xC8, 0xF4 });
            Assert.Throws<ArgumentOutOfRangeException>(() => mbbsEmuCpuCore.Run());
        }

        [Fact]
        public void CallAndJmp_IndirectRegister_WorkInFallbackPath()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuCpuRegisters.SP = 0x1000;
            mbbsEmuCpuRegisters.AX = 0x0020;
            mbbsEmuCpuRegisters.BX = 0x0030;

            // Place at 0xFFF2 so it executes via TickFallback()
            // FF D0 (CALL AX) -> 0x0020: FF E3 (JMP BX) -> 0x0030: F4 (HLT)
            mbbsEmuMemoryCore.SetArray(1, 0xFFF2, new byte[] { 0xFF, 0xD0 });
            mbbsEmuMemoryCore.SetArray(1, 0x0020, new byte[] { 0x26, 0xFF, 0xE3 }); // ES: JMP BX (forces fallback at low IP too)
            mbbsEmuMemoryCore.SetArray(1, 0x0030, new byte[] { 0xF4 });

            mbbsEmuCpuRegisters.IP = 0xFFF2;
            mbbsEmuCpuCore.Run();

            Assert.Equal(0x0031, mbbsEmuCpuRegisters.IP);
            Assert.Equal(0x0FFE, mbbsEmuCpuRegisters.SP);
            Assert.Equal(0xFFF4, mbbsEmuMemoryCore.GetWord(0, 0x0FFE));
        }

        [Fact]
        public void Movsb_Movsw_Cmpsb_HonorSegmentOverride_And_SelfOverlap_InFallbackPath()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(1);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DirectionFlag = false;

            // Put 0x42 at ES:[0] (segment 3) and 0x99 at DS:[0] (segment 2)
            mbbsEmuMemoryCore.SetByte(3, 0, 0x42);
            mbbsEmuMemoryCore.SetByte(2, 0, 0x99);

            // Place ES: REP MOVSB (26 F3 A4) at 0xFFF2 so it executes via TickFallback()
            // With SI = 0, DI = 1, CX = 4 in segment 3 (self-overlapping RLE fill of 0x42 across bytes 1..4)
            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.DI = 1;
            mbbsEmuCpuRegisters.CX = 4;
            mbbsEmuCpuRegisters.IP = 0xFFF2;
            mbbsEmuMemoryCore.SetArray(1, 0xFFF2, new byte[] { 0x26, 0xF3, 0xA4, 0xF4 });
            mbbsEmuCpuCore.Run();

            for (ushort i = 0; i < 5; i++)
                Assert.Equal(0x42, mbbsEmuMemoryCore.GetByte(3, i));

            // Test ES: CMPSB (26 A6) comparing ES:[SI] vs ES:[DI] when DS differs
            mbbsEmuCpuRegisters.Halt = false;
            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.DI = 1;
            mbbsEmuCpuRegisters.ZeroFlag = false;
            mbbsEmuCpuRegisters.IP = 0;
            mbbsEmuMemoryCore.SetArray(1, 0, new byte[] { 0x26, 0xA6, 0xF4 });
            mbbsEmuCpuCore.Run();

            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Fact]
        public void CallFar_ExternalFunction_ControlTransferToNewCsSameIp_PreservesTargetCsIp()
        {
            var mem = new ProtectedModeMemoryCore(logger: null);
            mem.AddSegment(1);
            mem.AddSegment(2);
            using var cpu = new CpuCore(logger: null);
            cpu.Reset(mem, (ordinal, fn) =>
            {
                // Transfer control to segment 2 at the exact same offset (0x0010)
                cpu.Registers.CS = 2;
                cpu.Registers.IP = 0x0010;
                return ReadOnlySpan<byte>.Empty;
            }, null, null);

            cpu.Registers.CS = 1;
            cpu.Registers.IP = 0x0010;
            // 9A 01 00 FE FF (CALL FAR 0xFFFE:0x0001) at 1:0x0010, and F4 (HLT) at 2:0x0010
            mem.SetArray(1, 0x0010, new byte[] { 0x9A, 0x01, 0x00, 0xFE, 0xFF });
            mem.SetArray(2, 0x0010, new byte[] { 0xF4 });

            cpu.Run();

            Assert.Equal(2, cpu.Registers.CS);
            Assert.Equal(0x0011, cpu.Registers.IP);
        }

        [Fact]
        public void RealMode_GetMemorySegment_ReturnsLogicalLength()
        {
            var realMemory = new RealModeMemoryCore(logger: null);
            Assert.Equal(RealModeMemoryCore.MAX_REAL_MODE_MEMORY + 65536, realMemory.GetMemorySegment(0).Length);
        }
    }
}
