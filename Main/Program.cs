using Main;
using NAudio.Wave;
using Main.ChatAgents;
using Main.Recorders.MultiSource;
using Main.Recorders.MultiSource.RecordingSources;
using Main.Transcribers.Whisper;
using Main.ChatAgents.OpenRouter;

// PARAMETERS
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

// SETUP
// Recorder
var recorder = new MultiSourceRecorder(waveFormat);
recorder.AddSource(new MicrophoneRecordingSource(waveFormat))
    .AddSource(new AudioRecordingSource(waveFormat));


// Transcriber
var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", "ggml-base.bin"), waveFormat); // model file downloadable from https://huggingface.co/ggerganov/whisper.cpp/tree/main

// Chat agent
var chatAgent = new ChatAgent(new OpenRouterChatClient());

//
var speechAnalyzer = new SpeechAnalyzer();
speechAnalyzer
    .UseRecorder(recorder)
    .UseTranscriber(transcriber)
    .UseChatAgent(chatAgent);

await speechAnalyzer.Process(sourceLanguage: "en", cleanUp: true, concise: true, targetLanguage: "anglais", additionalInstructions: [
    "le texte est destiné à une formation en ligne et le ton doit donc être relativement formel",
    "n'hésite pas à reformuler et/ou réordonner les phrases pour qu'elles soient plus claires et concises",
]);
// await transcriptor.Process(TranscriptionSource.MicrophoneOnly, sourceLanguage: "fr", cleanUp: true, concise: false, targetLanguage: "japonais", additionalInstructions: null);
