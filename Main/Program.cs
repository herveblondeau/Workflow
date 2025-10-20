using Main;
using NAudio.Wave;
using Main.Transcribers;
using Main.TextTransformers;
using Main.ChatAgents;
using Main.ChatAgents.OpenRouter;
using Main.Recorders;
using Whisper.net.Ggml;
using System.Diagnostics;

// try
// {
//     // Step 1: Get the default sink
//     string defaultSink = RunCommand("pactl", "info | grep 'Default Sink:' | awk '{print $3}'").Trim();
//     if (string.IsNullOrEmpty(defaultSink))
//     {
//         Console.WriteLine("Could not detect default sink.");
//         return;
//     }

//     // Step 2: Convert sink name to monitor source
//     string monitorSource = defaultSink + ".monitor";
//     Console.WriteLine($"Detected monitor source: {monitorSource}");

//     // Step 3: Build FFmpeg arguments to output raw PCM to stdout
//     string ffmpegArgs = $"-f pulse -i {monitorSource} -ac 2 -ar 44100 -f wav -";

//     Console.WriteLine("Starting recording. Press Ctrl+C to stop.");

//     using (Process ffmpeg = new Process())
//     {
//         ffmpeg.StartInfo.FileName = "ffmpeg";
//         ffmpeg.StartInfo.Arguments = ffmpegArgs;
//         ffmpeg.StartInfo.UseShellExecute = false;
//         ffmpeg.StartInfo.RedirectStandardOutput = true; // redirect stdout to read stream
//         ffmpeg.StartInfo.RedirectStandardError = true;  // redirect stderr to console
//         ffmpeg.StartInfo.CreateNoWindow = true;

//         ffmpeg.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };

//         ffmpeg.Start();
//         ffmpeg.BeginErrorReadLine();

//         // Step 4: Read stdout in real-time into a MemoryStream
//         MemoryStream audioStream = new MemoryStream();
//         CancellationTokenSource cts = new CancellationTokenSource();

//         Task readTask = Task.Run(async () =>
//         {
//             byte[] buffer = new byte[4096];
//             try
//             {
//                 int bytesRead;
//                 while (!cts.Token.IsCancellationRequested &&
//                     (bytesRead = await ffmpeg.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
//                 {
//                     audioStream.Write(buffer, 0, bytesRead);
//                 }
//             }
//             catch (OperationCanceledException)
//             {
//                 // Expected when user stops recording; exit gracefully
//             }
//         }, cts.Token);

//         Console.WriteLine("Recording... Press ENTER to stop.");
//         Console.ReadLine();

//         // Stop reading and kill FFmpeg
//         cts.Cancel();
//         if (!ffmpeg.HasExited)
//             ffmpeg.Kill();

//         await readTask;

//         Console.WriteLine($"Recording stopped. Captured {audioStream.Length} bytes of audio.");

//         // Example: write to file
//         File.WriteAllBytes("output.wav", audioStream.ToArray());
//         Console.WriteLine("Saved to output.wav");
//     }
// }
// catch (Exception ex)
// {
//     Console.WriteLine("Error: " + ex.Message);
// }

// static string RunCommand(string command, string arguments)
// {
//     ProcessStartInfo psi = new ProcessStartInfo
//     {
//         FileName = "/bin/bash",
//         Arguments = $"-c \"{command} {arguments}\"",
//         RedirectStandardOutput = true,
//         UseShellExecute = false,
//         CreateNoWindow = true
//     };

//     using (Process process = Process.Start(psi))
//     {
//         return process.StandardOutput.ReadToEnd();
//     }
// }

// Setup
int sampleRate = 16000;
int nbChannels = 1;
int bitsPerSample = 16;
var waveFormat = new WaveFormat(sampleRate, nbChannels, bitsPerSample);
var sourceLanguage = "en";
var transcriberModel = GgmlType.Base;

// Run
//var recorder = new WindowsAudioRecorder();
//var recorder = new WindowsMicrophoneRecorder();
// var recorder = new LinuxAudioRecorder();
// var recorder = new LinuxMicrophoneRecorder();

var recorder = new MultiSourceRecorder();
// recorder.AddSource(new LinuxAudioRecorder());
// recorder.AddSource(new LinuxMicrophoneRecorder());
//recorder.AddSource(new WindowsAudioRecorder());
recorder.AddSource(new WindowsMicrophoneRecorder());

var transcriber = new WhisperTranscriber(Path.Combine("d:/Temp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);
//var transcriber = new WhisperTranscriber(Path.Combine("/home/tigrou/tmp", $"whisper-model-{transcriberModel.ToString().ToLower()}.bin"), sourceLanguage, transcriberModel);

var chatClient = new OpenRouterChatClient();
chatClient.UseModel("google/gemini-2.5-flash-image");
var textProcessor = new AITextTransformer(new ChatAgent(chatClient), sourceLanguage);
textProcessor.AddInstruction("This is a transcription of an audio recording.")
    .AddInstruction("Can you write a summary of the main points discussed in the recording?")
;

var speechToTextProcessor = new SpeechToTextProcessor();
speechToTextProcessor
    .UseRecorder(recorder)
    .UseTranscriber(transcriber)
    .UseTextTransformer(textProcessor);

Console.WriteLine(await speechToTextProcessor.Process(sampleRate, nbChannels, bitsPerSample));
