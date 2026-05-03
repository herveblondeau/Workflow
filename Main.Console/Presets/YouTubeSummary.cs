using Core;
using Core.Models;
using Core.Tools.Workflow;
using FluentResults;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.ChatClients;
using Infrastructure.Downloaders;
using Infrastructure.TextTransformers;
using Infrastructure.Transcribers;
using Whisper.net.Ggml;

namespace Main.Console.Presets;

/// <summary>
/// Processes the content of a YouTube video given its URL
/// </summary>
public class YouTubeSummary
{
    private string _openRouterApiKey;

    public YouTubeSummary(string openRouterApiKey)
    {
        _openRouterApiKey = openRouterApiKey;
    }

    /// <summary>
    /// Summarizes the content
    /// </summary>
    public Task<Result<string>> Summarize(string url, string sourceLanguage, CancellationToken cancellationToken = default)
    {
        return Summarize(url, sourceLanguage, "Can you write a summary?", cancellationToken);
    }

    /// <summary>
    /// Sends a custom question
    /// </summary>
    public async Task<Result<string>> Summarize(string url, string sourceLanguage, string customQuestion, CancellationToken cancellationToken = default)
    {
        var audioFormat = new AudioFormat(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);
        var transcriberModel = GgmlType.Base;

        var chatClient = new OpenRouterChatClient(_openRouterApiKey);
        chatClient.UseModel("google/gemini-2.5-flash-image");

        var workflow = Workflow
            .Add(FirstSuccessfulTool
                .Add(new YouTubeSubtitlesDownloader(sourceLanguage))
                .Add(SequentialTool
                    .Add(new YouTubeAudioDownloader(audioFormat))
                    .Add(new WhisperTranscriber(audioFormat, sourceLanguage, transcriberModel))
                )
            )
            .Add(new AITextTransformer(new ChatAgent(chatClient), sourceLanguage, new List<string>
            {
                "This is a transcription of a Youtube video",
                customQuestion,
            }))
        ;

        return await workflow.Execute(url, cancellationToken);
    }
}
