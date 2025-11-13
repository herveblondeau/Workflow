using Core;
using Core.Models;
using FluentResults;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.OpenRouter;
using Infrastructure.Recorders;
using Infrastructure.TextTransformers;
using Infrastructure.Transcribers;
using Whisper.net.Ggml;

namespace Main.Console.Presets;

/// <summary>
/// Records the microphone and audio, and summarizes the content
/// </summary>
public class MeetingMinutes
{
    private string _openRouterApiKey;
    private Func<CancellationToken, Task>? _waitForStopSignal { get; set; }

    public MeetingMinutes(string openRouterApiKey, Func<CancellationToken, Task>? waitForStopSignal)
    {
        _openRouterApiKey = openRouterApiKey;
        _waitForStopSignal = waitForStopSignal;
    }

    /// <summary>
    /// Summarizes the content
    /// </summary>
    public Task<Result<string>> Summarize(string sourceLanguage,  CancellationToken cancellationToken = default)
    {
        return Summarize(sourceLanguage, "Can you write a summary?", cancellationToken); }

    /// <summary>
    /// Sends a custom question
    /// </summary>
    public Task<Result<string>> Summarize(string sourceLanguage, string customInstruction, CancellationToken cancellationToken = default)
    {
        return Summarize(sourceLanguage, new List<string> { customInstruction }, cancellationToken);
    }

    /// <summary>
    /// Sends a custom question
    /// </summary>
    public async Task<Result<string>> Summarize(string sourceLanguage, IEnumerable<string> customInstructions, CancellationToken cancellationToken = default)
    {
        var audioFormat = new AudioFormat(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);
        var transcriberModel = GgmlType.Base;

        var chatClient = new OpenRouterChatClient(_openRouterApiKey);
        chatClient.UseModel("google/gemini-2.5-flash-image");

        var multiSourceRecorder = new MultiSourceRecorder(audioFormat)
            .AddStopSignal(_waitForStopSignal!);

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
                multiSourceRecorder
                    .AddSource(new WindowsMicrophoneRecorder(audioFormat))
                    .AddSource(new WindowsAudioRecorder(audioFormat));
        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
                multiSourceRecorder
                    .AddSource(new LinuxMicrophoneRecorder(audioFormat))
                    .AddSource(new LinuxAudioRecorder(audioFormat));
        }
        else
        {
            throw new Exception("Unsupported OS platform for audio recording");
        }

        var workflow = Workflow
            .Add(multiSourceRecorder)
            .Add(new WhisperTranscriber(audioFormat, sourceLanguage, transcriberModel))
            .Add(new AITextTransformer(new ChatAgent(chatClient), sourceLanguage, customInstructions.Prepend("This is a transcription of a professional meeting (it could be a business meeting, an interview, a workshop etc.)")))
        ;

        return await workflow.Execute(Unit.Value, cancellationToken);
    }
}
