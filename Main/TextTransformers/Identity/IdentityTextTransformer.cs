namespace Main.TextTransformers.Identity
{
    public class IdentityTextTransformer : ITextTransformer
    {
        public Task<string> Process(string input)
        {
            return Task.FromResult(input);
        }
    }
}
