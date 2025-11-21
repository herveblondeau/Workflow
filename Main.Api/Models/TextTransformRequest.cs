namespace Main.Api.Models;

public class TextTransformRequest
{
    public string Instructions { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string Text { get; set; } = string.Empty;
}
