using FluentAssertions;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Testing;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Phapi
{
    /// <summary>
    ///     Covers PHAPI's DosRealIntr (ordinal 49) handling of INT 7Bh (Btrieve), which modules
    ///     that talk to Btrieve directly (bypassing MAJORBBS's own Btrieve helpers) use to
    ///     Open/Close/Stat .DAT files.
    /// </summary>
    public class DosRealIntr_Btrieve_Tests : PhapiTestBase
    {
        [Fact]
        public void Open_PopulatesPositionBlockAndActivatesFile()
        {
            var posBlock = OpenDatabase(DATABASE_A);

            var btvFileStruct = new BtvFileStruct(mbbsEmuMemoryCore.GetArray(posBlock, BtvFileStruct.Size));

            btvFileStruct.reclen.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
            btvFileStruct.filenam.Should().NotBe(FarPtr.Null);
            btvFileStruct.data.Should().NotBe(FarPtr.Null);

            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(btvFileStruct.filenam, stripNull: true))
                .Should().Be(DATABASE_A);

            // Open activates the file it just opened
            mbbsEmuMemoryCore.GetPointer("BB").Should().Be(posBlock);

            CloseDatabase(posBlock);
        }

        [Fact]
        public void Stat_ReturnsFileSpecForOpenedDatabase()
        {
            var posBlock = OpenDatabase(DATABASE_A);

            var fileSpec = StatDatabase(posBlock);

            fileSpec.reclen.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
            fileSpec.numofx.Should().Be(4);
            fileSpec.numofr.Should().Be(4);

            CloseDatabase(posBlock);
        }

        [Fact]
        public void SetOwner_IsIgnoredAndSucceeds()
        {
            var posBlock = OpenDatabase(DATABASE_A);

            var ax = Btrieve(EnumBtrieveOperationCodes.SetOwner, posBlock);

            ax.Should().Be(0);

            CloseDatabase(posBlock);
        }

        [Fact]
        public void UnsupportedOperation_Throws()
        {
            var posBlock = OpenDatabase(DATABASE_A);

            Assert.Throws<Exception>(() => Btrieve(EnumBtrieveOperationCodes.Insert, posBlock));

            CloseDatabase(posBlock);
        }

        /// <summary>
        ///     Regression coverage for the specific flow that used to be broken: opening two
        ///     separate files, closing the most-recently opened one (B), and then stat-ing the
        ///     first one (A). Before Close identified the file to close/BB by the caller's own
        ///     position block, and Stat only ever read the globally "active" (BB) file instead of
        ///     the position block the caller passed in, this exact sequence would stat the wrong
        ///     (already-closed) file.
        /// </summary>
        [Fact]
        public void OpenA_OpenB_CloseB_StatA_StillReturnsAsFileSpec()
        {
            var posBlockA = OpenDatabase(DATABASE_A);
            var posBlockB = OpenDatabase(DATABASE_B);

            // Opening B makes it the active (BB) file
            mbbsEmuMemoryCore.GetPointer("BB").Should().Be(posBlockB);

            CloseDatabase(posBlockB);

            // Closing the active file clears BB
            mbbsEmuMemoryCore.GetPointer("BB").Should().Be(FarPtr.Null);

            // A is untouched by B's open/close and can still be stat-ed successfully
            var fileSpecA = StatDatabase(posBlockA);
            fileSpecA.reclen.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
            fileSpecA.numofx.Should().Be(4);
            fileSpecA.numofr.Should().Be(4);

            CloseDatabase(posBlockA);
        }

        /// <summary>
        ///     Mirror of the flow above: closing a file that is NOT the active (BB) one must not
        ///     clear BB or otherwise disturb the file that's still open.
        /// </summary>
        [Fact]
        public void OpenA_OpenB_CloseA_LeavesBActiveAndStatable()
        {
            var posBlockA = OpenDatabase(DATABASE_A);
            var posBlockB = OpenDatabase(DATABASE_B);

            CloseDatabase(posBlockA);

            // B is still active since A (not B) was closed
            mbbsEmuMemoryCore.GetPointer("BB").Should().Be(posBlockB);

            var fileSpecB = StatDatabase(posBlockB);
            fileSpecB.reclen.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);

            CloseDatabase(posBlockB);
        }
    }
}
