using System.ComponentModel.DataAnnotations;
using Infrastructure.Tools.Watermarking;

namespace Main.Api.Models;

public class WatermarkRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;

    [Required]
    public WatermarkType WatermarkType { get; init; }

    [Required]
    public WatermarkContentType ContentType { get; init; }

    [MaxLength(200)]
    public string? CustomText { get; init; }

    public WatermarkPosition Position { get; init; } = WatermarkPosition.Diagonal;

    [Range(8, 96)]
    public int FontSize { get; init; } = 36;

    [Range(0.05f, 1.0f)]
    public float Opacity { get; init; } = 0.3f;

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$")]
    public string Color { get; init; } = "#FF0000";

    public DeliveryMode? DeliveryMode { get; init; }
}
