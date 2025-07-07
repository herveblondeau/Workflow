namespace Main;

public interface IRecorder : IDisposable
{
    void SetUp();
    void StartRecording();
    void StopRecording();
    int Read(byte[] buffer, int offset, int count);
    IBufferReader GetBufferReader();
}

public interface IBufferReader : IDisposable
{
    int Read(byte[] buffer, int offset, int count);
}
