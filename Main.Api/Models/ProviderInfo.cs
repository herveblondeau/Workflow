namespace Main.Api.Models;

public class ProviderInfo
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<ModelInfo> Models { get; set; } = [];
}
