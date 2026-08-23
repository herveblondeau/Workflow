using Infrastructure.Tools.Watermarking;
using Main.Api.Models;
using Main.Api.Watermarking.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Main.Api.Watermarking;

public class DiskWatermarkPipeline(
    IFileStore fileStore,
    ITokenStore tokenStore,
    PdfWatermarker watermarker,
    IConfiguration configuration,
    ILogger<DiskWatermarkPipeline> logger) : IWatermarkPipeline
{
    public Task<IActionResult> ProcessAsync(
        Stream input,
        string originalFileName,
        WatermarkOptions options,
        DeliveryMode? modeOverride = null)
    {
        var outputPath = fileStore.GetNewFilePath();
        try
        {
            using var fileStream = File.Create(outputPath);
            watermarker.Watermark(input, fileStream, options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Watermarking failed for file {Name}", originalFileName);
            fileStore.Delete(outputPath);
            IActionResult err = new ObjectResult(new { error = "Failed to process the PDF.", code = "PROCESSING_FAILED" })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return Task.FromResult(err);
        }

        var expiryHours = configuration.GetValue<int>("Filigrane:TokenExpiryHours", 4);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(expiryHours);
        var token = new Storage.DownloadToken
        {
            Token = Guid.NewGuid().ToString("N"),
            FilePath = outputPath,
            OriginalFileName = originalFileName,
            ExpiresAt = expiresAt
        };
        tokenStore.Add(token);

        IActionResult result = new AcceptedResult((string?)null, new
        {
            token = token.Token,
            downloadUrl = $"/api/download/{token.Token}",
            expiresAt,
            expiresInSeconds = (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds
        });
        return Task.FromResult(result);
    }
}
