namespace Core.Recorders;

public interface IStreamRecorder : IDisposable
{
    void Start(int sampleRate, int nbChannels, int bitsPerSample);
    Task Stop();
    Stream GetRecordedStream();
}
