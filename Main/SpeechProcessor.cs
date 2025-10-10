using Main.Transcribers;
using Main.Recorders;
using Main.TextTransformers;

namespace Main;

public class SpeechProcessor
{
    private IRecorder _recorder = null!;
    private ITranscriber _transcriber = null!;
    private ITextTransformer _textProcessor = null!;

    public SpeechProcessor UseRecorder(IRecorder recorder)
    {
        _recorder = recorder;
        return this;
    }

    public SpeechProcessor UseTranscriber(ITranscriber transcriber)
    {
        _transcriber = transcriber;
        return this;
    }

    public SpeechProcessor UseTextTransformer(ITextTransformer textProcessor)
    {
        _textProcessor = textProcessor;
        return this;
    }

    public async Task Process(string sourceLanguage, bool cleanUp, bool concise, List<string>? additionalInstructions = null, string? targetLanguage = null)
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
        var transcription = await _transcriber.Transcribe(recordedStream, sourceLanguage);
        await File.WriteAllTextAsync("transcription.txt", transcription);
        return;

        // 3) TEXT PROCESSING
        var result = _textProcessor.Process(transcription);
        Console.WriteLine(result);
        return;

        //// 3) AI PROCESSING
        //var prompt = $"Le texte suivant est une transcription d'un audio: \"{transcription}\". Merci de le transformer selon les instructions suivantes : {_buildInstructions(cleanUp, concise, targetLanguage, additionalInstructions)}\n";
        //_chatAgent.InitializeConversation();
        //var response = await _chatAgent.Prompt(prompt, supplyHistory: false);

        //Console.WriteLine(response);
        //return;

        //// 4) UTILS
        //string _buildInstructions(bool cleanUp = true, bool concise = true, string? language = null, List<string>? additionalInstructions = null)
        //{
        //    StringBuilder stringBuilder = new();

        //    if (cleanUp)
        //    {
        //        stringBuilder.Append("- il faut nettoyer les coquilles et les tics de langage\n");
        //    }

        //    if (concise)
        //    {
        //        stringBuilder.Append("- il faut le reformuler pour supprimer les répétitions et tournures redondantes\n");
        //    }

        //    if (language is not null)
        //    {
        //        stringBuilder.Append($"- il faut le traduire en {language}\n");
        //    }

        //    if (additionalInstructions is not null && additionalInstructions.Count > 0)
        //    {
        //        foreach (var instruction in additionalInstructions)
        //        {
        //            stringBuilder.Append($"- {instruction}\n");
        //        }
        //    }

        //    // stringBuilder.Append($"Pas besoin d'introduction, commentaires ou étapes intermédiaires, merci de donner uniquement le texte final.");

        //    return stringBuilder.ToString();
        //}
    }

}
