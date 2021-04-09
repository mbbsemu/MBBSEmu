namespace MBBSEmu.CPU
{
    public interface IIOPort
    {
        byte In(byte channel);
        void Out(byte channel, byte b);
    }
}
