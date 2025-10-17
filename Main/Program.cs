using Main;
using NAudio.Wave;
using Main.Transcribers;
using Main.TextTransformers;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;
using Main.Recorders;
using NAudio.CoreAudioApi;

// PARAMETERS
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
var language = "fr";

/*
// SETUP
var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
var audioCapture = new WasapiLoopbackCapture(device);
var audioBuffer = new MemoryStream();
//Console.WriteLine($"System audio format: {_audioCapture.WaveFormat}");
audioCapture.DataAvailable += (s, e) =>
{
    if (e.BytesRecorded > 0)
    {
        // Console.WriteLine($"System audio received {e.BytesRecorded} bytes");
        audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
    }
};

audioCapture.StartRecording();

Console.Write($"Recording started... Press ENTER to stop...");
Console.ReadLine();

audioCapture.StopRecording();
Thread.Sleep(100);
audioCapture.Dispose();
audioBuffer.Position = 0;



using (var writer = new WaveFileWriter("temp.wav", audioCapture.WaveFormat))
{
    audioBuffer.CopyTo(writer);
}


return;
*/















// Recorder
var recorder = new AudioRecorder(waveFormat);
await recorder.SetUp();
await recorder.Start();
Console.Write($"Recording started... Press ENTER to stop...");
Console.ReadLine();
var recordedStream = await recorder.Stop();
using (var writer = new WaveFileWriter("temp.wav", waveFormat))
{
    recordedStream.CopyTo(writer);
}
recorder.Dispose();

return;





















// Transcriber
var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", "ggml-base.bin"), waveFormat, language); // model file downloadable from https://huggingface.co/ggerganov/whisper.cpp/tree/main

// Text processor
//var textProcessor = new EmptyTextTransformer();
var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), "fr");
textProcessor.AddInstruction("le texte vient d'une vidéo YouTube")
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
