using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class prf_NullCharacter_Tests : ExportedModuleTestBase
    {
        protected override void Reset()
        {
            base.Reset();
            var buffer = mbbsEmuMemoryCore.GetVariablePointer("PRFBUF");
            mbbsEmuMemoryCore.SetPointer("PRFPTR", buffer);
            mbbsEmuMemoryCore.SetByte(buffer, 0);
        }

        [Theory]
        [InlineData(474, "%c", 0, "")]
        [InlineData(474, "A%cB", 0, "AB")]
        [InlineData(474, "A%3cB", 0, "A   B")]
        [InlineData(474, "A%-3cB", 0, "A   B")]
        [InlineData(474, "A%cB", 256, "AB")]
        [InlineData(474, "A%cB", 65, "AAB")]
        [InlineData(474, "A%3cB", 65, "A  AB")]
        [InlineData(476, "A%cB", 0, "AB")]
        [InlineData(178, "A%cB", 0, "AB")]
        public void PrfFamily_ZeroCharacterIsAnEmptyField(int ordinal, string format, int character, string expected)
        {
            Reset();
            Append(ordinal, format, (ushort)character);

            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
            Assert.Equal(mbbsEmuMemoryCore.GetVariablePointer("PRFBUF") + expected.Length,
                mbbsEmuMemoryCore.GetPointer("PRFPTR"));
        }

        [Fact]
        public void Prf_ZeroCharacterDoesNotTruncateSubsequentOutput()
        {
            Reset();
            majorbbs.SetState(0);
            testSessions[0].SessionVariables.Clear();

            Append(474, "Before");
            Append(474, "%c", 0);
            Append(474, "After");
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, 463, new List<ushort> { 0 });

            Assert.Equal("BeforeAfter", Encoding.ASCII.GetString(((TestSession)testSessions[0]).DrainSentData()));
        }

        [Theory]
        [InlineData(560)] // sprintf
        [InlineData(643)] // vsprintf
        public void CFormatting_PreservesAndCountsZeroCharacter(int ordinal)
        {
            Reset();
            var destination = mbbsEmuMemoryCore.Malloc(16);
            var format = StoreString("A%cB");
            var parameters = new List<ushort>
            {
                destination.Offset, destination.Segment, format.Offset, format.Segment
            };
            if (ordinal == 643)
            {
                var arguments = mbbsEmuMemoryCore.Malloc(2);
                mbbsEmuMemoryCore.SetWord(arguments, 0);
                parameters.Add(arguments.Offset);
                parameters.Add(arguments.Segment);
            }
            else
            {
                parameters.Add(0);
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal, parameters);

            Assert.Equal(new byte[] { (byte)'A', 0, (byte)'B', 0 },
                mbbsEmuMemoryCore.GetArray(destination, 4).ToArray());
            Assert.Equal(3, mbbsEmuCpuRegisters.AX);
        }

        private FarPtr StoreString(string text)
        {
            var pointer = mbbsEmuMemoryCore.Malloc((ushort)(text.Length + 1));
            mbbsEmuMemoryCore.SetArray(pointer, Encoding.ASCII.GetBytes(text + "\0"));
            return pointer;
        }

        private void Append(int ordinal, string format, params ushort[] arguments)
        {
            List<ushort> parameters;
            if (ordinal == 474)
            {
                var pointer = StoreString(format);
                parameters = new List<ushort> { pointer.Offset, pointer.Segment };
            }
            else
            {
                var mcv = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                    new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(format + "\0") } }));
                mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcv));
                parameters = new List<ushort> { 0 };
            }
            parameters.AddRange(arguments);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, (ushort)ordinal, parameters);
        }
    }
}
