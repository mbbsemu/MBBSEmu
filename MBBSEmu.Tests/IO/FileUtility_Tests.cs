using MBBSEmu.IO;
using System;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.IO
{
    public class FileUtility_Tests : IDisposable
    {
        private readonly FileUtility _fileUtility = FileUtility.CreateForTest();
        private readonly string _modulePath = Path.Join(Path.GetTempPath(), "testModule");

        public FileUtility_Tests()
        {
            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, /* recursive=*/ true);
        }

        [Theory]
        // all found
        [InlineData("file.txt", "file.txt", "file.txt")]
        [InlineData("file.txt", "fiLe.txt", "file.txt")]
        [InlineData("file.txt", "FILE.txt", "file.txt")]
        [InlineData("file.txt", "FILE.TXT", "file.txt")]
        [InlineData("dir1/dir2/dir3/file.txt", "dir1/dir2/dir3/file.txt", "dir1/dir2/dir3/file.txt")]
        [InlineData("dir1/dir2/dir3/file.txt", "Dir1\\dIr2\\diR3\\fiLe.txt", "dir1/dir2/dir3/file.txt")]
        [InlineData("Dir1/dIr2/diR3/File.txt", "dir1/dir2\\dir3/file.txt", "Dir1/dIr2/diR3/File.txt")]
        // not found
        [InlineData("file.txt", "file1.TXT", "file1.TXT")]
        [InlineData("dir1/dir2/dir3/file.txt", "dir1/dir2/dir3/file1.txt", "dir1/dir2/dir3/file1.txt")]
        [InlineData("dir1/dir2/dir3/file.txt", "dir1/dIr2/dir6/file.txt", "dir1/dir2/dir6/file.txt")]
        [InlineData("dir1/Dir2/dir66/file.txt", "Dir1/dir2/dir6/file.txt", "dir1/Dir2/dir6/file.txt")]
        // found, relative stripped
        [InlineData("file.txt", "./FILE.TXT", "file.txt")]
        [InlineData("file.txt", ".\\FILE.TXT", "file.txt")]
        public void FindFile_Test(string fileToCreate, string fileToSearchFor, string expected)
        {
            CreateFile(fileToCreate);

            // replace slashes with the system slash
            expected = expected.Replace('/', Path.DirectorySeparatorChar);

            Assert.Equal(expected, _fileUtility.FindFile(_modulePath, fileToSearchFor));
        }

        private void CreateFile(string file)
        {
            // replace slashes with the system slash
            file = file.Replace('/', Path.DirectorySeparatorChar);

            string path = Path.Join(_modulePath, file);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (StreamWriter writer = new System.IO.StreamWriter(path))
            {
                writer.WriteLine("Testing\r\n");
            }
        }
    }
}
