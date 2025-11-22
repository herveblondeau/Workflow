namespace Main.Api.Models;

public class URLTransformRequest
{
    public string Instructions { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string Text { get; set; } = string.Empty;
}
