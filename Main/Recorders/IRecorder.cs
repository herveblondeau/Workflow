namespace Main.Recorders;

public interface IRecorder : IDisposable
{
    public RecorderState State { get; }

    void Start(int sampleRate, int nbChannels, int bitsPerSample);
    Task Stop();
    Stream? GetRecordedStream();
}
