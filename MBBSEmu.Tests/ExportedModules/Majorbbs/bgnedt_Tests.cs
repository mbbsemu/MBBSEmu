using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class bgnedt_Tests : ExportedModuleTestBase
    {
        private const int BGNEDT_ORDINAL = 1191;

        [Theory]
        [InlineData("one\rtwo\rthree", new[] { "one", "two", "three" })]
        [InlineData("one\r\ntwo\r\nthree\r\n", new[] { "one", "two", "three", "" })]
        [InlineData("one\ntwo", new[] { "one", "two" })]
        [InlineData("one\r\rthree", new[] { "one", "", "three" })]
        public void BGNEDT_SplitsLines_Test(string text, string[] expectedLines)
        {
            Reset();

            var answers = ExecuteBgnedt(text);

            var expected = new StringBuilder("TOPIC=topic\0");
            for (var i = 0; i < expectedLines.Length; i++)
                expected.Append($"LINE{i}={expectedLines[i]}\0");
            expected.Append('\0');

            Assert.Equal(expected.ToString(), answers);
        }

        [Fact]
        public void BGNEDT_WrapsLongLines_Test()
        {
            Reset();

            var longLine = new string('A', 79) + new string('B', 79) + "CC";
            var answers = ExecuteBgnedt($"{longLine}\rnext");

            Assert.Equal(
                $"TOPIC=topic\0LINE0={new string('A', 79)}\0LINE1={new string('B', 79)}\0LINE2=CC\0LINE3=next\0\0",
                answers);
        }

        private string ExecuteBgnedt(string text)
        {
            const string topic = "topic";

            //Only the answers buffer is under test, don't render the template to the session
            testSessions[0].OutputEnabled = false;

            var textPointer = mbbsEmuMemoryCore.AllocateVariable("TEXT", 0x800);
            mbbsEmuMemoryCore.SetArray(textPointer, Encoding.ASCII.GetBytes(text + "\0"));
            var topicPointer = mbbsEmuMemoryCore.AllocateVariable("TOPIC", 0x50);
            mbbsEmuMemoryCore.SetArray(topicPointer, Encoding.ASCII.GetBytes(topic + "\0"));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, BGNEDT_ORDINAL,
                new List<ushort>
                {
                    0x800,
                    textPointer.Offset,
                    textPointer.Segment,
                    0x50,
                    topicPointer.Offset,
                    topicPointer.Segment,
                    0,
                    0,
                    0
                });

            var answersPointer = mbbsEmuMemoryCore.GetVariablePointer("FSD-Answers-0");
            var answers = mbbsEmuMemoryCore.GetArray(answersPointer, 0x800).ToArray();

            // Answers are double null terminated
            var end = 0;
            while (end + 1 < answers.Length && !(answers[end] == 0 && answers[end + 1] == 0))
                end++;

            return Encoding.ASCII.GetString(answers, 0, end + 2);
        }
    }
}
