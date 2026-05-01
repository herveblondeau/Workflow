namespace Main.Api.Models;

public class TextTransformRequest
{
    public string Provider { get; set; } = null!;
    public string Model { get; set; } = null!;

    public string Instructions { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string Text { get; set; } = string.Empty;
}
