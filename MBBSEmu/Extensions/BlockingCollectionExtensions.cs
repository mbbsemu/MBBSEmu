using System.Collections.Concurrent;

namespace MBBSEmu.Extensions
{
    public static class BlockingCollectionExtensions
    {
	public static void Clear<T>(this BlockingCollection<T> blockingCollection)
	{
	    while (blockingCollection.Count > 0)
	    {
		blockingCollection.TryTake(out _);
	    }
	}
    }
}
