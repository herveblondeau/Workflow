using Core;
using Core.Models;
using FluentResults;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.ChatClients;
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
    public Task<Result<string>> Summarize(string sourceLanguage, CancellationToken cancellationToken = default)
    {
        return Summarize(sourceLanguage, MeetingType.Business, null, cancellationToken);
    }

    /// <summary>
    /// Summarizes the content
    /// </summary>
    public Task<Result<string>> Summarize(string sourceLanguage, IEnumerable<string> customInstructions, CancellationToken cancellationToken = default)
    {
        return Summarize(sourceLanguage, MeetingType.Business, customInstructions, cancellationToken);
    }

    /// <summary>
    /// Summarizes the content
    /// </summary>
    public async Task<Result<string>> Summarize(string sourceLanguage, MeetingType meetingType = MeetingType.Business, IEnumerable<string>? customInstructions = null, CancellationToken cancellationToken = default)
    {
        var instructions = _getInstructions(meetingType, customInstructions);

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
            .Add(new AITextTransformer(new ChatAgent(chatClient), sourceLanguage, instructions))
        ;

        return await workflow.Execute(Unit.Value, cancellationToken);
    }

    private List<string> _getInstructions(MeetingType meetingType, IEnumerable<string>? customInstructions)
    {
        var instructions = new List<string>();
        if (customInstructions is not null)
        {
            instructions.AddRange(customInstructions);
        }
        else
        {
            switch (meetingType)
            {
                case MeetingType.Business:
                    instructions.Add("This is a transcription of a business meeting.");
                    instructions.Add("If possible:");
                    instructions.Add("- Summarize the main discussion points, decisions made, and action items assigned.");
                    instructions.Add("- Include any important deadlines or follow-up tasks.");
                    instructions.Add("- If possible, identify key participants and their contributions.");
                    instructions.Add("- Format the summary in clear bullet points for easy reference.");
                    break;
                case MeetingType.Interview:
                    instructions.Add("This is a transcription of a job interview.");
                    instructions.Add("If possible:");
                    instructions.Add("- Summarize the candidate's qualifications, experience, and key responses.");
                    instructions.Add("- Summarize the key points presented by the interviewer about the company, role and expectations.");
                    instructions.Add("- Give me feedback on the candidate's communication skills and overall impression.");
                    instructions.Add("- Give me feedback on the interviewer's questions and professionalism.");
                    instructions.Add("- Tell me if you think the candidate is a good fit for the role based on the discussion.");
                    break;
                case MeetingType.Workshop:
                    instructions.Add("This is a transcription of a workshop session.");
                    instructions.Add("If possible:");
                    instructions.Add("- Summarize the key topics covered, activities conducted, and any conclusions reached.");
                    instructions.Add("- Highlight any practical tips or techniques shared during the workshop.");
                    break;
            }
        }

        return instructions;
    }

    public enum MeetingType
    {
        Business,
        Interview,
        Workshop,
    }
}
