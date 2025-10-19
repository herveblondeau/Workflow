using Main.Transcribers;
using Main.Recorders;
using Main.TextTransformers;
using NAudio.Wave;

namespace Main;

public class SpeechToTextProcessor
{
    private IRecorder _recorder = null!;
    private ITranscriber _transcriber = null!;
    private ITextTransformer _textTransformer = null!;

    public SpeechToTextProcessor UseRecorder(IRecorder recorder)
    {
        _recorder = recorder;
        return this;
    }

    public SpeechToTextProcessor UseTranscriber(ITranscriber transcriber)
    {
        _transcriber = transcriber;
        return this;
    }

    public SpeechToTextProcessor UseTextTransformer(ITextTransformer textTransformer)
    {
        _textTransformer = textTransformer;
        return this;
    }

    public async Task<string> Process(WaveFormat waveFormat)
    {
        if (_recorder is null)
        {
            throw new ArgumentNullException("Recorder is undefined");
        }

        if (_transcriber is null)
        {
            throw new ArgumentNullException("Transcriber is undefined");
        }

        if (_textTransformer is null)
        {
            throw new ArgumentNullException("Text processor is undefined");
        }

        using (_recorder)
        {
            // 1) RECORDING
            _recorder.Start(waveFormat);

            Console.Write($"Recording started... Press ENTER to stop...");
            Console.ReadLine();

            Console.Write($"Stopping recording...");
            var recordedStream = _recorder.Stop();
            Console.WriteLine($" Done");

            // 2) TRANSCRIPTION
            Console.Write($"Transcribing...");
            var transcription = await _transcriber.Transcribe(recordedStream);
            Console.WriteLine($" Done");
            await File.WriteAllTextAsync("transcription.txt", transcription);

            // 3) TEXT TRANSFORMATION
            Console.Write($"Transforming...");
            var result = await _textTransformer.Process(transcription);
            Console.WriteLine($" Done");

            return result;
        }
    }
}
