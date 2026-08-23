using Infrastructure.Tools.Watermarking;
using Main.Api.Models;
using Main.Api.Watermarking;
using Main.Api.Watermarking.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Main.Api;

[ApiController]
[Route("api")]
public class WatermarkController(
    IWatermarkPipeline pipeline,
    ITokenStore tokenStore,
    IFileStore fileStore) : ControllerBase
{
    private static readonly HashSet<string> AllowedMimeTypes = ["application/pdf"];
    private static readonly byte[] PdfMagicBytes = "%PDF"u8.ToArray();

    [HttpPost("watermark")]
    [RequestSizeLimit(4_194_304)]
    public async Task<IActionResult> UploadAndWatermark([FromForm] WatermarkRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request parameters.", code = "INVALID_OPTIONS", details = ModelState });

        if (!AllowedMimeTypes.Contains(request.File.ContentType))
            return BadRequest(new { error = "Only PDF files are accepted.", code = "INVALID_FILE_TYPE" });

        using var peekStream = request.File.OpenReadStream();
        var magic = new byte[4];
        if (await peekStream.ReadAsync(magic) < 4 || !magic.SequenceEqual(PdfMagicBytes))
            return BadRequest(new { error = "File does not appear to be a valid PDF.", code = "INVALID_FILE_TYPE" });
        peekStream.Seek(0, SeekOrigin.Begin);

        if (request.ContentType == WatermarkContentType.Custom && string.IsNullOrWhiteSpace(request.CustomText))
            return BadRequest(new { error = "customText is required when contentType is 'custom'.", code = "INVALID_OPTIONS" });

        var options = new WatermarkOptions
        {
            Type = request.WatermarkType,
            ContentType = request.ContentType,
            CustomText = request.CustomText,
            Position = request.Position,
            FontSize = request.FontSize,
            Opacity = request.Opacity,
            Color = request.Color
        };

        return await pipeline.ProcessAsync(peekStream, SanitizeFileName(request.File.FileName), options, request.DeliveryMode);
    }

    [HttpGet("download/{token}")]
    public IActionResult Download(string token)
    {
        var entry = tokenStore.Get(token);

        if (entry is null)
            return NotFound(new { error = "Download link not found.", code = "NOT_FOUND" });

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            tokenStore.Remove(token);
            fileStore.Delete(entry.FilePath);
            return StatusCode(410, new { error = "This download link has expired.", code = "EXPIRED" });
        }

        if (!tokenStore.TryConsume(token, out var consumed) || consumed is null)
            return NotFound(new { error = "Download link not found or already used.", code = "NOT_FOUND" });

        if (!System.IO.File.Exists(consumed.FilePath))
            return StatusCode(500, new { error = "File not found on server.", code = "PROCESSING_FAILED" });

        var fileName = Path.GetFileNameWithoutExtension(consumed.OriginalFileName) + "-watermarked.pdf";
        var stream = new FileStream(consumed.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 65536, useAsync: true);

        var deletingStream = new DeletingFileStream(stream, consumed.FilePath, fileStore);
        tokenStore.Remove(token);

        return File(deletingStream, "application/pdf", fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "document.pdf" : clean;
    }
}

file sealed class DeletingFileStream(FileStream inner, string path, IFileStore fileStore) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        inner.ReadAsync(buffer, offset, count, ct);

    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        fileStore.Delete(path);
        base.Dispose(disposing);
    }
}
