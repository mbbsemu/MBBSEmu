using System.Threading.Tasks;

namespace MBBSEmu.Resources
{
    public interface IResourceManager
    {
        /// <summary>
        ///     Gets the specified Embedded Resource and returns it as a string
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string GetString(string key);
    }
}
