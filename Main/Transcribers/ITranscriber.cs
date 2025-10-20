namespace Main.Transcribers;

public interface ITranscriber
{
    Task<string> Transcribe(Stream inputStream, int sampleRate, int nbChannels, int bitsPerSample);
}
