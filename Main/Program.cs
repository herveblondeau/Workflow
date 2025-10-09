using Main;
using NAudio.Wave;
using Main.ChatAgents;
using Main.Recorders;
using Main.Transcribers;

// SETUP
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
var source = TranscriptionSource.MicrophoneOnly;

// Recorder
var recorder = new BufferedRecorder(waveFormat);
if (source == TranscriptionSource.MicrophoneOnly || source == TranscriptionSource.MicrophoneAndAudio)
{
    recorder.AddSource(new MicrophoneRecordingSource(waveFormat));
}
if (source == TranscriptionSource.AudioOnly || source == TranscriptionSource.MicrophoneAndAudio)
{
    recorder.AddSource(new AudioRecordingSource(waveFormat));
}

// Transcriber
var transcriber = new WhisperTranscriber("models/ggml-small.bin");

// Chat agent
var chatAgent = new ChatAgent(new OpenRouterChatClient());

//
var speechAnalyzer = new SpeechAnalyzer();
speechAnalyzer.UseRecorder(recorder);
speechAnalyzer.UseTranscriber(transcriber);
speechAnalyzer.UseChatAgent(chatAgent);

await speechAnalyzer.Process(TranscriptionSource.MicrophoneOnly, sourceLanguage: "fr", cleanUp: true, concise: true, targetLanguage: "anglais", additionalInstructions: [
    "le texte est destiné à une formation en ligne et le ton doit donc être relativement formel",
    "n'hésite pas à reformuler et/ou réordonner les phrases pour qu'elles soient plus claires et concises",
]);
// await transcriptor.Process(TranscriptionSource.MicrophoneOnly, sourceLanguage: "fr", cleanUp: true, concise: false, targetLanguage: "japonais", additionalInstructions: null);
