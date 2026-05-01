namespace Main.Api.Models;

public class ImageTransformMetadata
{
    public string Provider { get; set; } = null!;
    public string Model { get; set; } = null!;

    public string Instructions { get; set; } = string.Empty;
    public string? Language { get; set; }
    public double? CssWidth { get; set; }
    public double? CssHeight { get; set; }
    public double? PixelWidth { get; set; }
    public double? PixelHeight { get; set; }
}
