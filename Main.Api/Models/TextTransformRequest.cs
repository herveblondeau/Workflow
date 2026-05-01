namespace Main.Api.Models;

public class TextTransformRequest : ChatRequest
{
    public string Text { get; set; } = string.Empty;
}
