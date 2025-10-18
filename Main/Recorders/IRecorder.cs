using NAudio.Wave;

namespace Main.Recorders;

public interface IRecorder : IDisposable
{
    void Start(WaveFormat targetFormat);
    Stream Stop();
}
public interface IBufferableRecorder : IRecorder
{
    IBufferReader GetBufferReader();
}

public interface IBufferReader : IDisposable
{
    int Read(byte[] buffer, int offset, int count);
}
