using System.Text;
using Whisper.net;
using NAudio.Wave;
using Whisper.net.Ggml;
using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.Transcribers;

public class WhisperTranscriber : ITool<Stream, string>
{
    private readonly string _modelFilePath;
    private readonly string _language;
    private readonly GgmlType _modelType;
    private readonly AudioFormat _audioFormat;

    public WhisperTranscriber(
        AudioFormat audioFormat,
        string modelFilePath,
        string language,
        GgmlType modelType = GgmlType.Base)
    {
        _audioFormat = audioFormat;
        _modelFilePath = modelFilePath;
        _language = language;
        _modelType = modelType;
    }

    public async Task<Result<string>> Transform(Stream input, CancellationToken cancellationToken = default)
    {
        var tempFile = $"{Path.GetTempFileName()}.wav";

        input.Position = 0;
        using (var writer = new WaveFileWriter(tempFile, new WaveFormat(_audioFormat.SampleRate, _audioFormat.BitsPerSample, _audioFormat.NbChannels)))
        {
            input.CopyTo(writer);
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
        using (var fileStream = File.OpenRead(tempFile))
        {
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                transcription.Append(result.Text);
            }
        }

        // Clean up
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

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
