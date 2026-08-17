namespace MBBSEmu.Logging.Targets
{
    public interface ILoggingTarget
    {
        public void Write(params object[] logEntry);
    }
}
