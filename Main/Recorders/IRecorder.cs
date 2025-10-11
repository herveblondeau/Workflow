namespace Main.Recorders;

public interface IRecorder : IDisposable
{
    Task SetUp();
    Task Start();
    Task<Stream> Stop();
}
