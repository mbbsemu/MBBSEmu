using Xunit;

namespace MBBSEmu.Tests.UI
{
    /// <summary>
    ///     Tests that create a Terminal.Gui application share this collection so they never run in parallel
    /// </summary>
    [CollectionDefinition("TerminalGui", DisableParallelization = true)]
    public class TerminalGuiCollection
    {
    }
}
