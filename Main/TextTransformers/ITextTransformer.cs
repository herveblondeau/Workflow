namespace Main.TextTransformers;

public interface ITextTransformer
{
    Task<string> Process(string input);
}
