using Core;
using Core.Models;
using Core.Tools.Workflow;
using FluentResults;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.OpenRouter;
using Infrastructure.Downloaders;
using Infrastructure.TextTransformers;
using Infrastructure.Transcribers;
using Whisper.net.Ggml;

namespace Main.Console.Presets;

public class YouTubeSummary
{
    public Task<Result<string>> Summarize(string url, string sourceLanguage, CancellationToken cancellationToken = default)
    {
        return Summarize(url, sourceLanguage, "Can you write a summary?", cancellationToken);
    }

    public async Task<Result<string>> Summarize(string url, string sourceLanguage, string customQuestion, CancellationToken cancellationToken = default)
    {
        var audioFormat = new AudioFormat(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);
        var transcriberModel = GgmlType.Base;

        var chatClient = new OpenRouterChatClient("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");
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
