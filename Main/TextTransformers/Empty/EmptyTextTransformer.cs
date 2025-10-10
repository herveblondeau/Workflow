namespace Main.TextTransformers.Empty;

public class EmptyTextTransformer : ITextTransformer
{
    public Task<string> Process(string input)
    {
        return Task.FromResult(input);
    }
}
