namespace Infrastructure.Tools.Watermarking;

public enum WatermarkType { Visible, Invisible, Both }
public enum WatermarkContentType { Timestamp, Custom }
public enum WatermarkPosition
{
    Diagonal,
    TopLeft, TopCenter, TopRight,
    Center,
    BottomLeft, BottomCenter, BottomRight
}

public record WatermarkOptions
{
    public WatermarkType Type { get; init; }
    public WatermarkContentType ContentType { get; init; }
    public string? CustomText { get; init; }
    public WatermarkPosition Position { get; init; } = WatermarkPosition.Diagonal;
    public int FontSize { get; init; } = 36;
    public float Opacity { get; init; } = 0.3f;
    public string Color { get; init; } = "#FF0000";
}
