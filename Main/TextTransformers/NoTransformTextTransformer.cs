namespace Main.TextTransformers;

public class NoTransformTextTransformer : ITextTransformer
{
    public Task<string> Process(string input)
    {
        return Task.FromResult(input);
    }
}
