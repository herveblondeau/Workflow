using Main;
using Main.Transcribers;
using Main.TextTransformers;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;
using Main.Recorders;
using Whisper.net.Ggml;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using Main.Downloaders;
using System.Diagnostics;
using System.Text.Json;
using Main.Extensions;
using System.Net;
using System.Text.Json.Serialization;

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
var stream = await downloader.Download(videoUrl, sampleRate, nbChannels, bitsPerSample);
Console.WriteLine("Done");
Console.Write("Transcribing...");
var transcription = await transcriber.Transcribe(stream, sampleRate, nbChannels, bitsPerSample);
Console.WriteLine("Done");
Console.Write("Processing...");
var finalText = await textProcessor.Process(transcription);
Console.WriteLine("Done");
Console.WriteLine(finalText);
return;

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