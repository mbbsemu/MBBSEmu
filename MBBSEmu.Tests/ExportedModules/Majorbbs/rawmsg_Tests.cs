using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rawmsg_Tests : ExportedModuleTestBase
    {
        private const int RAWMSG_ORDINAL = 487;

        [Theory]
        [InlineData(0, "Normal")]
        [InlineData(1, "")]
        [InlineData(2, "123456")]
        [InlineData(3, "--==---")]
        [InlineData(4, "!@)#!*$")]
        public void rawmsg_Test(ushort ordinal, string msgValue)
        {
            //Reset State
            Reset();

            //Build a Test Dictionary containing all incorrect values
            var incorrectValues = new Dictionary<int, byte[]>
            {
                { 0, "INCORRECT"u8.ToArray() },
                { 1, "INCORRECT"u8.ToArray() },
                { 2, "INCORRECT"u8.ToArray() },
                { 3, "INCORRECT"u8.ToArray() },
                { 4, "INCORRECT"u8.ToArray() },
                { 5, "INCORRECT"u8.ToArray() },
                { 6, "INCORRECT"u8.ToArray() },
                { 7, "INCORRECT"u8.ToArray() },
                { 8, "INCORRECT"u8.ToArray() },
                { 9, "INCORRECT"u8.ToArray() },
                { 10, "INCORRECT"u8.ToArray() },
                { 11, "INCORRECT"u8.ToArray() },
                { 12, "INCORRECT"u8.ToArray() },
                { 13, "INCORRECT"u8.ToArray() },
                { 14, "INCORRECT"u8.ToArray() },
                { 15, "INCORRECT"u8.ToArray() },
                { 16, "INCORRECT"u8.ToArray() },
                { 17, "INCORRECT"u8.ToArray() },
                { 18, "INCORRECT"u8.ToArray() },
                { 19, "INCORRECT"u8.ToArray() },
                { 20, "INCORRECT"u8.ToArray() }
            };

            //Set Input Ordinal In Dictionary to Correct Value
            incorrectValues[ordinal] = Encoding.ASCII.GetBytes(msgValue);

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                incorrectValues));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RAWMSG_ORDINAL, new List<ushort> { ordinal });

            //Verify Results
            Assert.Equal(msgValue, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));
        }
    }
}