using System.IO;
using System;
using System.Text;
using Whisper.net;
namespace Main.Transcribers;

public class WhisperTranscriber : ITranscriber
{
    private readonly string _modelFilePath;

    public WhisperTranscriber(string modelFilePath)
    {
        _modelFilePath = modelFilePath;
    }

    public async Task<string> Transcribe(Stream inputStream, string language)
    {
        // Initialize Whisper
        // https://github.com/sandrohanea/whisper.net?tab=readme-ov-file
        var whisperFactory = WhisperFactory.FromPath(_modelFilePath);
        var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        StringBuilder transcription = new();
        using (inputStream)
        {
            await foreach (var result in processor.ProcessAsync(inputStream))
            {
                transcription.Append(result.Text);
            }
        }

        return transcription.ToString();
    }
}
