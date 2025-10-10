namespace Main.Transcribers;

public interface ITranscriber
{
    Task<string> Transcribe(Stream inputStream, string inputLanguage);
}
