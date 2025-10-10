using Main.Transcribers;
using Main.Recorders;
using Main.TextTransformers;

namespace Main;

public class SpeechToTextProcessor
{
    private IRecorder _recorder = null!;
    private ITranscriber _transcriber = null!;
    private ITextTransformer _textProcessor = null!;

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

    public SpeechToTextProcessor UseTextTransformer(ITextTransformer textProcessor)
    {
        _textProcessor = textProcessor;
        return this;
    }

    public async Task<string> Process()
    {
        if (_recorder is null)
        {
            throw new ArgumentNullException("Recorder is undefined");
        }

        if (_transcriber is null)
        {
            throw new ArgumentNullException("Transcriber is undefined");
        }

        if (_textProcessor is null)
        {
            throw new ArgumentNullException("Text processor is undefined");
        }

        // 1) RECORDING
        await _recorder.SetUp();
        await _recorder.Start();

        var startDateTime = DateTime.Now;
        Console.Write($"Recording started at {startDateTime:HH:mm:ss}... Press ENTER to stop...");
        Console.ReadLine();
        Console.WriteLine($"Stopping recording...");
        var recordedStream = await _recorder.Stop();
        var stopDateTime = DateTime.Now;
        Console.WriteLine($"Recording stopped at {stopDateTime:HH:mm:ss}");
        Console.WriteLine($"Recording duration: {(stopDateTime - startDateTime).TotalSeconds} seconds");

        _recorder.Dispose();

        // 2) TRANSCRIPTION
        var transcription = await _transcriber.Transcribe(recordedStream);
        await File.WriteAllTextAsync("transcription.txt", transcription);

        // 3) TEXT PROCESSING
        var result = await _textProcessor.Process(transcription);
        return result;
    }
}
