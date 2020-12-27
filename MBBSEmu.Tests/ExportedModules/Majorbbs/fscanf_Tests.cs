using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class fscanf_Tests : FileTestBase, IDisposable
    {
        private const int FSCANF_ORDINAL = 232;

        [Fact]
        public void sscanf_reads_records()
        {
            //Reset State
            Reset();

            const string FILE_CONTENTS = "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n"
                + "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n"
                + "{ 5 -2 4 testing abc }\r\n{ 5 -2 4 testing abc }\r\n";
            const string FORMAT = "{ %d %d %d %s %s }";

            var filePath = CreateTextFile("file.txt", FILE_CONTENTS);

            Assert.Equal(FILE_CONTENTS.Length, new FileInfo(filePath).Length);

            var filep = fopen("file.txt", "r");
            Assert.NotEqual(0, filep.Segment);
            Assert.NotEqual(0, filep.Offset);

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable(null, 16);
            mbbsEmuMemoryCore.SetArray(formatPointer, Encoding.ASCII.GetBytes(FORMAT));

            var intPointer1 = mbbsEmuMemoryCore.AllocateVariable("FIRST", 2);
            var intPointer2 = mbbsEmuMemoryCore.AllocateVariable("SECOND", 2);
            var intPointer3 = mbbsEmuMemoryCore.AllocateVariable("THIRD", 2);
            var strPointer1 = mbbsEmuMemoryCore.AllocateVariable("FOURTH", 32);
            var strPointer2 = mbbsEmuMemoryCore.AllocateVariable("FIFTH", 32);

            //Execute Test
            int lines = 0;
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<FarPtr> {
                    filep, formatPointer, intPointer1, intPointer2, intPointer3, strPointer1, strPointer2
                });
            Assert.Equal(5, mbbsEmuCpuRegisters.AX);

            while (mbbsEmuCpuRegisters.AX == 5)
            {
                ++lines;

                Assert.Equal(5, mbbsEmuMemoryCore.GetWord(intPointer1));
                Assert.Equal(0xFFFE, mbbsEmuMemoryCore.GetWord(intPointer2));
                Assert.Equal(4, mbbsEmuMemoryCore.GetWord(intPointer3));
                Assert.Equal("testing", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(strPointer1, stripNull: true)));
                Assert.Equal("abc", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(strPointer2, stripNull: true)));

                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FSCANF_ORDINAL, new List<FarPtr> {
                    filep, formatPointer, intPointer1, intPointer2, intPointer3, strPointer1, strPointer2
                });
            }

            Assert.Equal(0, fclose(filep));

            Assert.Equal(6, lines);
        }
    }
}
