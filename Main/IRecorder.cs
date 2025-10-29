namespace Main;

public interface IRecorder : IDisposable
{
    void Start(int sampleRate, int nbChannels, int bitsPerSample);
    Task Stop();
    Stream? GetRecordedStream();
}
