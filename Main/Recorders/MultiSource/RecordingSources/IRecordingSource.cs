namespace Main.Recorders.MultiSource.RecordingSources;

public interface IRecordingSource : IDisposable
{
    void SetUp();
    void StartRecording();
    void StopRecording();
    IBufferReader GetBufferReader(); // TODO: too specific; ideally should return something more generic like a stream
}

public interface IBufferReader : IDisposable
{
    int Read(byte[] buffer, int offset, int count);
}
