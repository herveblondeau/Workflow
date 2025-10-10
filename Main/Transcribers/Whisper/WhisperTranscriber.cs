using System.Text;
using Whisper.net;
using NAudio.Wave;
using Whisper.net.Ggml;

namespace Main.Transcribers.Whisper;

public class WhisperTranscriber : ITranscriber
{
    private readonly string _modelFilePath;
    private readonly WaveFormat _waveFormat;
    private readonly string _language;
    private const string TEMPORARY_WAV_FILE_NAME = "output.wav";

    public WhisperTranscriber(string modelFilePath, WaveFormat waveFormat, string language)
    {
        _modelFilePath = modelFilePath;
        _waveFormat = waveFormat;
        _language = language;
    }

    public async Task<string> Transcribe(Stream inputStream)
    {
        // Whisper cannot work from a Stream; it requires an actual WAV file to operate
        using (var writer = new WaveFileWriter(TEMPORARY_WAV_FILE_NAME, _waveFormat))
        {
            inputStream.CopyTo(writer);
        }

        // Initialize Whisper
        // https://github.com/sandrohanea/whisper.net?tab=readme-ov-file
        if (!File.Exists(_modelFilePath))
        {
            await _downloadModel(_modelFilePath, GgmlType.Base);
        }
        var whisperFactory = WhisperFactory.FromPath(_modelFilePath);
        var processor = whisperFactory.CreateBuilder()
            .WithLanguage(_language)
            .Build();

        StringBuilder transcription = new();
        using (var fileStream = File.OpenRead(TEMPORARY_WAV_FILE_NAME))
        {
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                transcription.Append(result.Text);
            }
        }

        File.Delete(TEMPORARY_WAV_FILE_NAME);

        return transcription.ToString();
    }
    private static async Task _downloadModel(string fileName, GgmlType ggmlType)
    {
        // Console.WriteLine($"Downloading Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }
}
