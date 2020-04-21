namespace MBBSEmu.IO
{
    public interface IFileUtility
    {
        string FindFile(string modulePath, string fileName);
        string CorrectPathSeparator(string fileName);
    }
}