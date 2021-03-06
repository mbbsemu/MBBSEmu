namespace MBBSEmu.IO
{
    public interface IFileUtility
    {
        string FindFile(string modulePath, string fileName);
        string ResolvePathWithWildcards(string modulePath, string filePath);
        string CorrectPathSeparator(string fileName);
    }
}