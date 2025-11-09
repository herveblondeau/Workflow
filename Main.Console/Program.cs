using Core;
using Core.ChatAgents;
using Whisper.net.Ggml;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Diagnostics;
using System.Text.Json;
using System.Net;
using System.Text.Json.Serialization;
using Infrastructure.Recorders;
using Infrastructure.Transcribers;
using Infrastructure.TextTransformers;
using Infrastructure.Downloaders;
using Core.Models;
using FluentResults;
using System.IO.Pipelines;
using Infrastructure.Workflow;

// var firstSuccessfulTool = new FirstSuccessfulTool<int, int>();
// firstSuccessfulTool
//     .Add(new Tool1())
//     .Add(new Tool2())
//     .Add(new Tool3())
//     .Add(new Tool4())
// ;

// var parallelTool = new ParallelTool<int, string>(
//     [
//         new ParallelTool<int, int>.ValueSubTool<int, int>(new ToolA()), // tool that needs an input
//         new ParallelTool<int, string>.ValueSubTool<int, string>(new ToolB()), // tool that needs an input
//         new ParallelTool<int, double>.ValueSubTool<int, double>(new ToolC()), // tool that needs an input
//         new ParallelTool<int, string>.UnitSubTool<int, string>(new ToolD()) // tool with no input
//     ],
//     outputs =>
//     {
//         var myInt = (int)outputs[0];
//         var myString = (string)outputs[1];
//         var myDouble = (double)outputs[2];
//         var myString2 = (string)outputs[3];
//         return "A string computed from all output results";
//     }
// );

var workflow = Workflow
    // .Add(parallelTool)
    .Add(new Tool1())
    .Add(new Tool2())
    .Add(new Tool3())
    .Add(new Tool4())
;
var result = await workflow.Execute(6);
if (result.IsFailed)
{
    Console.WriteLine("Workflow failed");
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error);
    }
    return;
}
Console.WriteLine($"Result: {result.Value}");
return;

class ToolA : ITool<int, int>
{
    public Task<Result<int>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 1 failed"));
        return Task.FromResult(Result.Ok(input));
    }
}

class ToolB : ITool<int, string>
{
    public Task<Result<string>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 1 failed"));
        return Task.FromResult(Result.Ok("TOOL B RESULT"));
    }
}

class ToolC : ITool<int, double>
{
    public Task<Result<double>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 1 failed"));
        return Task.FromResult(Result.Ok(0.5));
    }
}

class ToolD : ITool<Unit, string>
{
    public Task<Result<string>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 1 failed"));
        return Task.FromResult(Result.Ok("TOOL D RESULT"));
    }
}

// var workflow = Workflow
//     .Add(parallelTool)
// ;
// var result = await workflow.Execute(6);
// if (result.IsFailed)
// {
//     Console.WriteLine("Workflow failed");
//     foreach (var error in result.Errors)
//     {
//         Console.WriteLine(error);
//     }
//     return;
// }
// Console.WriteLine($"Result: {result.Value}");

class Tool1 : ITool<int, int>
{
    public Task<Result<int>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 1 failed"));
        return Task.FromResult(Result.Ok(input));
    }
}

class Tool2 : ITool<int, int>
{
    public Task<Result<int>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 2 failed"));
        return Task.FromResult(Result.Ok(input * 2));
    }
}

class Tool3 : ITool<int, int>
{
    public Task<Result<int>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 3 failed"));
        return Task.FromResult(Result.Fail<int>("Tool 3 failed"));
        return Task.FromResult(Result.Ok(input * 3));
    }
}

class Tool4 : ITool<int, int>
{
    public Task<Result<int>> Transform(int input, CancellationToken cancellationToken = default)
    {
        // return Task.FromResult(Result.Fail<int>("Tool 4 failed"));
        return Task.FromResult(Result.Ok(input * 4));
    }
}

class Tool5 : ITool<int, string>
{
    public Task<Result<string>> Transform(int input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Ok("Result is 36"));
    }
}

class Tool6 : ITool<int, string>
{
    public Task<Result<string>> Transform(int input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Ok("Result is NOT 36"));
    }
}

/*
var audioFormat = new AudioFormat(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;
var sourceUrl = "https://www.youtube.com/watch?v=2jH_pr8nGQU&pp=ugUEEgJlbg%3D%3D";

var chatClient = new OpenRouterChatClient("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");
chatClient.UseModel("google/gemini-2.5-flash-image");

var workflow = Workflow
    .Add(new YouTubeAudioDownloader(sourceUrl, audioFormat))
    .Add(new WhisperTranscriber(audioFormat, Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel))
    .Add(new AITextTransformer(new ChatAgent(chatClient), sourceLanguage, new List<string>
    {
        "This is a transcription of an audio recording.",
        "Can you write a summary of the main points discussed in the recording?",
        "The summary MUST be concise and to the point (MAXIMUM 100 words)",
    }))
;
var result = await workflow.Execute();
if (result.IsFailed)
{
    Console.WriteLine("Workflow failed");
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error.Message);
    }
    return;
}
Console.WriteLine(result.Value);
return;
*/

/*
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;
var sourceUrl = "https://www.youtube.com/watch?v=2jH_pr8nGQU&pp=ugUEEgJlbg%3D%3D";
string transcription;

try
{
    Console.Write("Downloading subtitles...");
    var subtitlesDownloader = new YouTubeSubtitlesDownloader();
    transcription = await subtitlesDownloader.ProcessAsync(new YouTubeSubtitlesDownloaderParams(sourceUrl, sourceLanguage), CancellationToken.None);
    Console.WriteLine("Done");
}
catch
{
    Console.WriteLine("Failed... Falling back to audio transcription");

    Console.Write("Downloading audio...");
    var audioDownloader = new YouTubeAudioDownloader();
    var stream = await audioDownloader.ProcessAsync(new YouTubeAudioDownloaderParams(sourceUrl, sampleRate, nbChannels, bitsPerSample));
    Console.WriteLine("Done");

    Console.Write("Transcribing...");
    var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);
    transcription = await transcriber.ProcessAsync(new(stream, sampleRate, nbChannels, bitsPerSample));
    Console.WriteLine("Done");
}

Console.Write("Processing");
var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of an audio recording.")
    .AddInstruction("Can you write a summary of the main points discussed in the recording?")
    .AddInstruction("The summary MUST be concise and to the point (MAXIMUM 100 words)")
;
var finalText = await textProcessor.ProcessAsync(transcription);
Console.WriteLine("Done");

Console.WriteLine();
Console.WriteLine(finalText);

return;
*/

/*
// Setup
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

// Run
var downloader = new YouTubeAudioDownloader("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

// var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);
var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of an audio recording.")
    .AddInstruction("Can you write a summary of the main points discussed in the recording?")
    .AddInstruction("The summary should be concise and to the point (maximum 200 words)")
;

Console.WriteLine("Downloading");
var stream = await downloader.Download();
Console.WriteLine("Transcribing");
var transcription = await transcriber.Transcribe(stream, sampleRate, nbChannels, bitsPerSample);
Console.WriteLine("Processing");
var finalText = await textProcessor.Process(transcription);
Console.WriteLine(finalText);
return;
*/

/*
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

var recorder = new MultiSourceRecorder();
recorder.WaitForStopSignal = async (cancellationToken) =>
{
    Console.Write("Recording started... Press ENTER to stop...");
    await Task.Run(() =>
    {
        Console.ReadLine();
        Console.WriteLine("Stopping recording...");
    }, cancellationToken);
};
recorder.AddSource(new WindowsAudioRecorder());
recorder.AddSource(new WindowsMicrophoneRecorder());

//var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);
var transcriber = new WhisperTranscriber(Path.Combine("D:\\Temp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of a Youtube video")
    .AddInstruction("Can you write a summary of the contents?")
    .AddInstruction("The summary should be concise and to the point (maximum 300 words)")
;

var stream = await recorder.ProcessAsync(new RecorderParams(16000, 1, 16));
Console.WriteLine("Done");
Console.Write("Transcribing...");
var transcription = await transcriber.ProcessAsync(new(stream, sampleRate, nbChannels, bitsPerSample));
Console.WriteLine("Done");
Console.WriteLine(transcription);
Console.Write("Processing...");
var finalText = await textProcessor.ProcessAsync(transcription);
Console.WriteLine("Done");
Console.WriteLine(finalText);
return;
*/

/*
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

var videoUrl = "https://www.youtube.com/watch?v=etgH5EWHwlc";
var downloader = new YouTubeAudioDownloader();

var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of a Youtube video")
    .AddInstruction("Can you write a summary of the contents?")
    .AddInstruction("The summary should be concise and to the point (maximum 300 words)")
;

Console.Write("Downloading...");
var stream = await downloader.ProcessAsync(new(videoUrl, sampleRate, nbChannels, bitsPerSample));
Console.WriteLine("Done");
Console.Write("Transcribing...");
var transcription = await transcriber.ProcessAsync(new(stream, sampleRate, nbChannels, bitsPerSample));
Console.WriteLine("Done");
Console.Write("Processing...");
var finalText = await textProcessor.ProcessAsync(transcription);
Console.WriteLine("Done");
Console.WriteLine(finalText);
return;
*/

/*
// Setup
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

// Run
//var recorder = new WindowsAudioRecorder();
//var recorder = new WindowsMicrophoneRecorder();
var recorder = new LinuxAudioRecorder();
// var recorder = new LinuxMicrophoneRecorder();

// var recorder = new MultiSourceRecorder();
// recorder.AddSource(new LinuxAudioRecorder());
// recorder.AddSource(new LinuxMicrophoneRecorder());
//recorder.AddSource(new WindowsAudioRecorder());
// recorder.AddSource(new WindowsMicrophoneRecorder());

// var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);
var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of an audio recording.")
    .AddInstruction("Can you write a summary of the main points discussed in the recording?")
    .AddInstruction("The summary should be concise and to the point (maximum 200 words)")
;

var speechToTextProcessor = new SpeechToTextProcessor();
speechToTextProcessor
    .UseRecorder(recorder)
    .UseTranscriber(transcriber)
    .UseTextTransformer(textProcessor);

Console.WriteLine(await speechToTextProcessor.Process(sampleRate, nbChannels, bitsPerSample));
*/