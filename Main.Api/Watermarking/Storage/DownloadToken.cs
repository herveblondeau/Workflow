namespace Main.Api.Watermarking.Storage;

public record DownloadToken
{
    public required string Token { get; init; }
    public required string FilePath { get; init; }
    public required string OriginalFileName { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public bool Downloaded { get; set; } = false;
}
