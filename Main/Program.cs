using Main;
using NAudio.Wave;
using Main.Transcribers;
using Main.TextTransformers;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;
using Main.Recorders;
using Whisper.net.Ggml;

// Setup
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

// Run
//var recorder = new AudioRecorder();
//var recorder = new MicrophoneRecorder();
var recorder = new MultiSourceRecorder();
recorder.AddSource(new WindowsAudioRecorder());
recorder.AddSource(new WindowsMicrophoneRecorder());

var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), waveFormat, sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("the text comes from a YouTube video")
    .AddInstruction("please summarize the contents in 300 words max")
;

var speechToTextProcessor = new SpeechToTextProcessor();
speechToTextProcessor
    .UseRecorder(recorder)
    .UseTranscriber(transcriber)
    .UseTextTransformer(textProcessor);

Console.WriteLine(await speechToTextProcessor.Process(waveFormat));
