using Main;
using NAudio.Wave;
using Main.Transcribers;
using Main.TextTransformers;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;
using Main.Recorders;
using NAudio.CoreAudioApi;
using Whisper.net.Ggml;

// PARAMETERS
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

// ACTUAL
var recorder = new AudioRecorder();
//var recorder = new MicrophoneRecorder();
//var recorder = new MultiSourceRecorder();
//recorder.AddSource(new AudioRecorder());
//recorder.AddSource(new MicrophoneRecorder());

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
return;

// TEST
/*
var recorder = new AudioRecorder();
recorder.Start(waveFormat);
Console.Write($"Recording started... Press ENTER to stop...");
Console.ReadLine();
var recordedStream = recorder.Stop();
//using (var writer = new WaveFileWriter("temp.wav", waveFormat))
//{
//    recordedStream.CopyTo(writer);
//}
//recorder.Dispose();

recordedStream.Position = 0;

var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", "ggml-base.bin"), waveFormat, sourceLanguage); // model file downloadable from https://huggingface.co/ggerganov/whisper.cpp/tree/main
Console.WriteLine(await transcriber.Transcribe(recordedStream));

return;
*/

/*
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
