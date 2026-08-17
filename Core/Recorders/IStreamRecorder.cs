namespace Core.Recorders;

public interface IStreamRecorder : IDisposable
{
    /// <summary>
    /// Begins capturing. Asynchronous because a recorder may have to interrogate the
    /// platform's audio server before it can open a source
    /// </summary>
    Task Start(int sampleRate, int nbChannels, int bitsPerSample);
    Task Stop();
    Stream? GetRecordedStream();
}
