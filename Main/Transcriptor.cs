using NAudio.Wave;
using System.Text;
using Whisper.net.Ggml;
using Whisper.net;
using Connectors.OpenRouter;
using Main.OpenRouter;
using Main.Recorders;

namespace Main
{
    public class Transcriptor
    {
        public async Task Process(TranscriptionSource source, string sourceLanguage, bool cleanUp, bool concise, List<string>? additionalInstructions = null, string? targetLanguage = null)
        {
            // 1) SETUP
            var targetFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
            var recorders = new List<IRecordingSource>();
            if (source == TranscriptionSource.MicrophoneOnly || source == TranscriptionSource.MicrophoneAndAudio)
            {
                recorders.Add(new MicrophoneRecordingSource(targetFormat));
            }
            if (source == TranscriptionSource.AudioOnly || source == TranscriptionSource.MicrophoneAndAudio)
            {
                recorders.Add(new AudioRecordingSource(targetFormat));
            }

            foreach (var recorder in recorders)
            {
                recorder.SetUp();
            }

            foreach (var recorder in recorders)
            {
                recorder.StartRecording();
            }

            var startDateTime = DateTime.Now;
            Console.Write($"Recording started at {startDateTime:HH:mm:ss}... Press ENTER to stop...");
            Console.ReadLine();

            var stopDateTime = DateTime.Now;
            Console.WriteLine($"Recording stopped at {startDateTime:HH:mm:ss}");

            foreach (var recorder in recorders)
            {
                recorder.StopRecording();
            }

            Console.WriteLine($"Recording duration: {(stopDateTime - startDateTime).TotalSeconds} seconds");

            // Save recording to file
            var outputAudioFile = "output.wav";
            Console.WriteLine("Saving...");
            // Console.Write("Save combined audio to " + outputFile + "...");
            // _saveStream(micRaw, targetFormat, outputFile);
            _saveRecordings(recorders, targetFormat, outputAudioFile);
            await Task.Delay(1000); // Wait for file to be written
            // Console.WriteLine("done");

            foreach (var recorder in recorders)
            {
                recorder.Dispose();
            }

            await Task.Delay(1000); // Wait for file to be written
            // 2) TRANSCRIPTION
            // Initialize Whisper
            // https://github.com/sandrohanea/whisper.net?tab=readme-ov-file
            var ggmlType = GgmlType.Base;
            var modelFileName = Path.Combine(Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName, "ggml-base.bin"); // downloadable from https://huggingface.co/ggerganov/whisper.cpp/tree/main
            if (!File.Exists(modelFileName))
            {
                await _downloadModel(modelFileName, ggmlType);
            }
            var whisperFactory = WhisperFactory.FromPath(modelFileName);
            var processor = whisperFactory.CreateBuilder()
                .WithLanguage(sourceLanguage)
                .Build();

            Console.WriteLine("Transcribing...");
            // Console.WriteLine("Transcribe recording from " + outputFile + "...");
            StringBuilder transcription = new();
            using (var fileStream = File.OpenRead(outputAudioFile))
            {
                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    // Console.WriteLine($"- {result.Text}");
                    transcription.Append(result.Text);
                }
            }
            File.Delete(outputAudioFile);
            await File.WriteAllTextAsync("transcription.txt", transcription.ToString());

            // Console.WriteLine("Done");
            // Console.WriteLine($"Transcription: {transcription}");

            // 3) PROCESSING
            Console.WriteLine("Processing...");
            OpenRouterChatClient client = new();
            client.UseModel("openai/gpt-4");
            ChatAgent agent = new(client);
            string response = null!;
            var prompt = $"Le texte suivant est une transcription d'un audio: \"{transcription}\". Merci de le transformer selon les instructions suivantes : {_buildInstructions(cleanUp, concise, targetLanguage, additionalInstructions)}\n";
            // Console.WriteLine($"Prompt: {prompt}");
            // Console.Write("Processing...");
            response = await agent.Prompt(prompt);
            // Console.WriteLine("Done");

            // Console.WriteLine("");
            // Console.WriteLine($"ORIGINAL TRANSCRIPTION");
            // Console.WriteLine(transcription);
            // Console.WriteLine();
            // Console.WriteLine($"EDITED TRANSCRIPTION");
            Console.WriteLine(response);
            // Console.WriteLine();
            return;

            // 4) UTILS
            void _saveRecordings(List<IRecordingSource> recorders, WaveFormat format, string outputPath)
            {
                int bytesPerSample = format.BitsPerSample / 8;
                int bufferSize = format.AverageBytesPerSecond / 10; // 100ms buffer
                var buffers = recorders.Select(r => new byte[bufferSize]).ToList();
                byte[] mixedBuffer = new byte[bufferSize];
                var bufferReaders = recorders.Select(r => r.GetBufferReader());

                using var writer = new WaveFileWriter(outputPath, format);
                while (true)
                {
                    var bytes = bufferReaders.Select((br, i) => br.Read(buffers[i], 0, bufferSize)).ToList();
                    if (bytes.All(b => b == 0))
                        break;

                    int maxBytes = bytes.Max();

                    for (int i = 0; i < maxBytes; i += bytesPerSample)
                    {
                        var samples = bytes.Select((_, n) => i < bytes[n] ? BitConverter.ToInt16(buffers[n], i) : (short)0).ToList();
                        short mixed = 0;
                        foreach (var sample in samples)
                        {
                            mixed += sample;
                        }
                        mixed = Math.Clamp(mixed, short.MinValue, short.MaxValue);

                        BitConverter.GetBytes((short)mixed).CopyTo(mixedBuffer, i);
                    }

                    writer.Write(mixedBuffer, 0, maxBytes);
                }
            }

            string _buildInstructions(bool cleanUp = true, bool concise = true, string? language = null, List<string>? additionalInstructions = null)
            {
                StringBuilder stringBuilder = new();

                if (cleanUp)
                {
                    stringBuilder.Append("- il faut nettoyer les coquilles et les tics de langage\n");
                }

                if (concise)
                {
                    stringBuilder.Append("- il faut le reformuler pour supprimer les répétitions et tournures redondantes\n");
                }

                if (language is not null)
                {
                    stringBuilder.Append($"- il faut le traduire en {language}\n");
                }

                if (additionalInstructions is not null && additionalInstructions.Count > 0)
                {
                    foreach (var instruction in additionalInstructions)
                    {
                        stringBuilder.Append($"- {instruction}\n");
                    }
                }

                // stringBuilder.Append($"Pas besoin d'introduction, commentaires ou étapes intermédiaires, merci de donner uniquement le texte final.");

                return stringBuilder.ToString();
            }
        }

        private static async Task _downloadModel(string fileName, GgmlType ggmlType)
        {
            // Console.WriteLine($"Downloading Model {fileName}");
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
            using var fileWriter = File.OpenWrite(fileName);
            await modelStream.CopyToAsync(fileWriter);
        }
    }

    public enum TranscriptionSource
    {
        MicrophoneOnly,
        AudioOnly,
        MicrophoneAndAudio,
    }
}
