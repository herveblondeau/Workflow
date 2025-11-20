namespace Main.Api.Models;

public class ImageTransformMetadata
{
    public string Instructions { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string Source { get; set; } = string.Empty;
    public double? CssWidth { get; set; }
    public double? CssHeight { get; set; }
    public int? PixelWidth { get; set; }
    public int? PixelHeight { get; set; }
}
