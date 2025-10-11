using Main;
using NAudio.Wave;
using Main.Recorders.MultiSource;
using Main.Recorders.MultiSource.RecordingSources;
using Main.Transcribers.Whisper;
using Main.TextTransformers.Empty;
using Main.TextTransformers.AI;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;

// PARAMETERS
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
var language = "fr";

// SETUP
// Recorder
var recorder = new MultiSourceRecorder(waveFormat);
recorder.AddSource(new AudioRecordingSource(waveFormat));

// Transcriber
var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", "ggml-base.bin"), waveFormat, language); // model file downloadable from https://huggingface.co/ggerganov/whisper.cpp/tree/main

// Text processor
//var textProcessor = new EmptyTextTransformer();
var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), "fr");
textProcessor.AddInstruction("le texte vient d'une vidéo YouTube")
    .AddInstruction("il faut supprimer les hésitations, mots parasites et tics de langage (euh..., alors... etc.)")
    .AddInstruction("il faut résumer le contenu en 300 mots maximum")
;

//
var speechToTextProcessor = new SpeechToTextProcessor();
speechToTextProcessor
    .UseRecorder(recorder)
    .UseTranscriber(transcriber)
    .UseTextTransformer(textProcessor);

var result = await speechToTextProcessor.Process();
Console.WriteLine(result);

