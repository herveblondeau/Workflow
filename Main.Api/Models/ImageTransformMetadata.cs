namespace Main.Api.Models;

public class ImageTransformMetadata : ChatRequest
{
    public double? CssWidth { get; set; }
    public double? CssHeight { get; set; }
    public double? PixelWidth { get; set; }
    public double? PixelHeight { get; set; }
}
