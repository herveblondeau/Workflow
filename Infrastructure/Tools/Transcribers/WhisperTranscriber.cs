using System.Text;
using Whisper.net;
using NAudio.Wave;
using Whisper.net.Ggml;
using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.Transcribers;

public class WhisperTranscriber : ITool<AudioStream, string>
{
    private readonly string _language;
    private readonly GgmlType _modelType;
    private readonly string _modelFilePath;
    private readonly AudioFormat _audioFormat;

    public WhisperTranscriber(
        AudioFormat audioFormat,
        string language,
        GgmlType modelType = GgmlType.Base)
    {
        _audioFormat = audioFormat;
        _language = language;
        _modelType = modelType;
        _modelFilePath = Path.Combine(Path.GetTempPath(), $"whisper-{modelType}.bin");
    }

    public async Task<Result<string>> Transform(AudioStream input, CancellationToken cancellationToken = default)
    {
        var tempWavFile = $"{Path.GetTempFileName()}.wav"; // Whisper requires an actual WAV file to work

        input.Position = 0;
        try
        {
            using (var writer = new WaveFileWriter(tempWavFile, new WaveFormat(_audioFormat.SampleRate, _audioFormat.BitsPerSample, _audioFormat.NbChannels)))
            {
                input.CopyTo(writer);
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(WhisperTranscriber)}: cannot write WAV file {tempWavFile}").CausedBy(ex));
        }

        // Initialize Whisper
        // https://github.com/sandrohanea/whisper.net?tab=readme-ov-file
        if (!File.Exists(_modelFilePath))
        {
            try
            {
                await _downloadModel(_modelType);
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"{nameof(WhisperTranscriber)}: cannot download model {_modelType}").CausedBy(ex));
            }
        }
        var whisperFactory = WhisperFactory.FromPath(_modelFilePath);
        var processor = whisperFactory.CreateBuilder()
            .WithLanguage(_language)
            .Build();

        // Transcribe
        StringBuilder transcription = new();
        try
        {
            using (var fileStream = File.OpenRead(tempWavFile))
            {
                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    transcription.Append(result.Text);
                }
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(WhisperTranscriber)}: cannot open file {tempWavFile}").CausedBy(ex));
        }
        Console.WriteLine(transcription);
        Console.WriteLine();

        // Clean up
        try
        {
            if (File.Exists(tempWavFile))
            {
                File.Delete(tempWavFile);
            }
        }
        catch {}

        return Result.Ok(transcription.ToString());
    }

    // Models are manually downlodable from https://huggingface.co/ggerganov/whisper.cpp/tree/main
    private async Task _downloadModel(GgmlType ggmlType)
    {
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(_modelFilePath);
        await modelStream.CopyToAsync(fileWriter);
    }
}
