namespace Main.Api.Models;

public abstract class ChatRequest
{
    public string Provider { get; set; } = null!;
    public string? Model { get; set; }
    public string? Instructions { get; set; }
    public string? Language { get; set; }
}
