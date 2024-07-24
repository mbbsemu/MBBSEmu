using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stpans_Tests : ExportedModuleTestBase
    {
        private const int STPANS_ORDINAL = 712;

        [Theory]
        [InlineData("\u001b[31mRed Text\u001b[0m", "Red Text")]
        [InlineData("\u001b[31;1mBold Red Text\u001b[22;39m", "Bold Red Text")]
        [InlineData("\u001b[31;44mRed on Blue\u001b[0m Text", "Red on Blue Text")]
        [InlineData("Normal \u001b[33mYellow Text\u001b[0m", "Normal Yellow Text")]
        [InlineData("\u001b[32mGreen\u001b[0m\u001b[33mYellow\u001b[0m", "GreenYellow")]
        [InlineData("No Color Here", "No Color Here")]
        [InlineData("\u001b[31m\u001b[32mNested \u001b[33mColors\u001b[0m", "Nested Colors")]
        [InlineData("\u001b[0;31;51;101mComplex \u001b[0mText", "Complex Text")]
        [InlineData("\u001b[1A", "")] // Cursor up
        [InlineData("\u001b[2J", "")] // Clear screen
        [InlineData("Text\u001b[0K", "Text")] // Clear line from cursor right
        [InlineData("\u001b[7mReverse\u001b[27m", "Reverse")] // Reverse video on and off
        [InlineData("\u001b]0;Title\u0007", "")] // OSC (Operating System Command) to set window title
        [InlineData("\u001b[?25h", "")] // Show cursor
        [InlineData("\u001b[?25l", "")] // Hide cursor
        [InlineData("Normal Text\u001b[10CAfter Tab", "Normal TextAfter Tab")] // Move cursor forward 10 spaces
        [InlineData("123456789\u001b[3D", "123456789")] // Move cursor back 3 spaces
        [InlineData("Start\u001b[s\u001b[uEnd", "StartEnd")] // Save cursor position and restore it
        public void STPANS_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STPANS_ORDINAL, new List<FarPtr> { stringPointer });
            var resultPointer = mbbsEmuCpuRegisters.GetPointer();

            //Verify Results
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}
