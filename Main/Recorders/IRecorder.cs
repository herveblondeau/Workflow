namespace Main.Recorders;

public interface IRecorder : IDisposable
{
    Task SetUp();
    Task Start();
    Task<Stream> Stop();
}
public interface IBufferableRecorder : IRecorder
{
    IBufferReader GetBufferReader();
}

public interface IBufferReader : IDisposable
{
    int Read(byte[] buffer, int offset, int count);
}
