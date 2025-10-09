namespace Main.Recorders;

public interface IRecorder
{
    Task SetUp();
    Task Start();
    Task<byte[]> Stop();
}
