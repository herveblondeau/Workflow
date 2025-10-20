using NAudio.Wave;

namespace Main.Transcribers;

public interface ITranscriber
{
    Task<string> Transcribe(Stream inputStream, WaveFormat waveFormat);
}
