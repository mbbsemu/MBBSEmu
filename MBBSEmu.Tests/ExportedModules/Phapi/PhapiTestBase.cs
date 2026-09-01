using MBBSEmu.Btrieve.Enums;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MBBSEmu.Tests.ExportedModules.Phapi
{
    public class PhapiTestBase : ExportedModuleTestBase, IDisposable
    {
        protected const int DOSREALINTR_ORDINAL = 49;
        protected const int DOSALLOCREALSEG_ORDINAL = 16;

        protected const byte INT_21H = 0x21;
        protected const byte INT_7BH = 0x7B;

        /// <summary>
        ///     Two independently named copies of the same Btrieve test asset, so tests can open
        ///     two distinct files ("A" and "B") at the same time
        /// </summary>
        protected const string DATABASE_A = "MBBSEMU.DAT";
        protected const string DATABASE_B = "MBBSEMU2.DAT";

        protected PhapiTestBase() : base(Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}"))
        {
            Directory.CreateDirectory(mbbsModule.ModulePath);
            CopyModuleFiles();
        }

        public override void Dispose()
        {
            base.Dispose();

            phapi.Dispose();

            Directory.Delete(mbbsModule.ModulePath, recursive: true);
        }

        private void CopyModuleFiles()
        {
            var resourceManager = ResourceManager.GetTestResourceManager();
            var sourceBytes = resourceManager.GetResource("MBBSEmu.Tests.Assets.MBBSEMU.DAT").ToArray();

            File.WriteAllBytes(Path.Combine(mbbsModule.ModulePath, DATABASE_A), sourceBytes);
            File.WriteAllBytes(Path.Combine(mbbsModule.ModulePath, DATABASE_B), sourceBytes);
        }

        /// <summary>
        ///     Invokes PHAPI ordinal 49 (DosRealIntr) for the given real-mode interrupt number,
        ///     loading the given register block into a fresh real-mode segment first.
        ///
        ///     Returns the register block as PHAPI left it (e.g. BX for a Get Interrupt Vector call).
        /// </summary>
        protected Regs16Struct DosRealIntr(byte interruptNumber, Regs16Struct regs)
        {
            var registerSegment = mbbsEmuMemoryCore.AllocateRealModeSegment(Regs16Struct.Size);
            mbbsEmuMemoryCore.SetArray(registerSegment, regs.Data);

            ExecuteApiTest(HostProcess.ExportedModules.Phapi.Segment, DOSREALINTR_ORDINAL, new List<ushort>
            {
                interruptNumber,
                registerSegment.Offset,
                registerSegment.Segment,
                0, // reserved, must be zero
                0, // wordCount
            });

            return new Regs16Struct(mbbsEmuMemoryCore.GetArray(registerSegment, Regs16Struct.Size).ToArray());
        }

        /// <summary>
        ///     Invokes PHAPI's INT 7Bh (Btrieve) handler with a BTVDAT block built from the given
        ///     fields, and returns AX (PHAPI's Btrieve operations never set an error code -- they
        ///     either succeed with AX == 0 or throw)
        /// </summary>
        protected ushort Btrieve(EnumBtrieveOperationCodes operation, FarPtr posBlk, FarPtr keyPointer = null,
                                 FarPtr dataBufferPointer = null, ushort dataBufferLength = 0)
        {
            keyPointer ??= FarPtr.Null;
            dataBufferPointer ??= FarPtr.Null;

            var btvda = new BtvdatStruct
            {
                funcno = (ushort)operation,
                posblkseg = posBlk.Segment,
                posblkoff = posBlk.Offset,
                keyseg = keyPointer.Segment,
                keyoff = keyPointer.Offset,
                databufsegment = dataBufferPointer.Segment,
                databufoffset = dataBufferPointer.Offset,
                databuflen = dataBufferLength,
            };

            // PHAPI reads the BTVDAT block unconditionally from DS:0000, so it must sit at the
            // very base of its own segment
            var dsSegment = mbbsEmuMemoryCore.AllocateRealModeSegment(BtvdatStruct.Size);
            mbbsEmuMemoryCore.SetArray(dsSegment, btvda.Data);

            DosRealIntr(INT_7BH, new Regs16Struct { DS = dsSegment.Segment });

            return mbbsEmuCpuRegisters.AX;
        }

        /// <summary>
        ///     Opens fileName via PHAPI's Btrieve Open and returns the newly allocated position block
        /// </summary>
        protected FarPtr OpenDatabase(string fileName)
        {
            var posBlk = mbbsEmuMemoryCore.Malloc(BtvFileStruct.Size);

            // PHAPI reads the filename unconditionally from keyseg:0000, so it must sit at the
            // very base of its own segment
            var fileNameSegment = mbbsEmuMemoryCore.AllocateRealModeSegment((ushort)(fileName.Length + 1));
            mbbsEmuMemoryCore.SetArray(fileNameSegment, Encoding.ASCII.GetBytes(fileName + "\0"));

            Btrieve(EnumBtrieveOperationCodes.Open, posBlk, keyPointer: fileNameSegment);

            return posBlk;
        }

        protected ushort CloseDatabase(FarPtr posBlk) => Btrieve(EnumBtrieveOperationCodes.Close, posBlk);

        protected BtvfilespecStruct StatDatabase(FarPtr posBlk)
        {
            var dataBuffer = mbbsEmuMemoryCore.Malloc(BtvstatfbStruct.Size);
            Btrieve(EnumBtrieveOperationCodes.Stat, posBlk, dataBufferPointer: dataBuffer, dataBufferLength: BtvstatfbStruct.Size);

            var raw = mbbsEmuMemoryCore.GetArray(dataBuffer, BtvstatfbStruct.Size).ToArray();
            return new BtvstatfbStruct(raw).fs;
        }
    }
}
