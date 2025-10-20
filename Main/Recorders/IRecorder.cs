namespace Main.Recorders;

public interface IRecorder : IDisposable
{
    void Start(int sampleRate, int nbChannels, int bitsPerSample);
    Task<Stream> Stop();
    Stream GetOutputStream();
}
