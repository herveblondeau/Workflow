using System.Text;
using Whisper.net;
using NAudio.Wave;
using Whisper.net.Ggml;

namespace Main.Transcribers;

public class WhisperTranscriber : ITranscriber
{
    private readonly string _modelFilePath;
    private readonly string _language;
    private readonly GgmlType _modelType;
    private const string TEMPORARY_WAV_FILE_NAME = "whisper_temp.wav";

    public WhisperTranscriber(string modelFilePath, string language, GgmlType modelType = GgmlType.Base)
    {
        _modelFilePath = modelFilePath;
        _language = language;
        _modelType = modelType;
    }

    public async Task<string> Transcribe(Stream inputStream, WaveFormat waveFormat)
    {
        // Whisper cannot work from a Stream; it requires an actual WAV file to operate
        inputStream.Position = 0;
        using (var writer = new WaveFileWriter(TEMPORARY_WAV_FILE_NAME, waveFormat))
        {
            inputStream.CopyTo(writer);
        }

        // Initialize Whisper
        // https://github.com/sandrohanea/whisper.net?tab=readme-ov-file
        if (!File.Exists(_modelFilePath))
        {
            await _downloadModel(_modelFilePath, _modelType);
        }
        var whisperFactory = WhisperFactory.FromPath(_modelFilePath);
        var processor = whisperFactory.CreateBuilder()
            .WithLanguage(_language)
            .Build();

        // Transcribe
        StringBuilder transcription = new();
        using (var fileStream = File.OpenRead(TEMPORARY_WAV_FILE_NAME))
        {
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                transcription.Append(result.Text);
            }
        }

        // Clean up
        File.Delete(TEMPORARY_WAV_FILE_NAME);

        return transcription.ToString();
    }

    // Models are manually downlodable from https://huggingface.co/ggerganov/whisper.cpp/tree/main
    private static async Task _downloadModel(string fileName, GgmlType ggmlType)
    {
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }
}
