namespace MBBSEmu.Memory
{
    public interface IGlobalCache
    {
        T Get<T>(string key);
        object Get(string key);
        bool Set(string key, object value);
        bool Remove(string key);
        bool ContainsKey(string key);
        bool TryGet<T>(string key, out T result);
    }
}