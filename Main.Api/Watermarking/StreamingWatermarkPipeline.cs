using Infrastructure.Tools.Watermarking;
using Main.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Main.Api.Watermarking;

public class StreamingWatermarkPipeline(
    PdfWatermarker watermarker,
    ILogger<StreamingWatermarkPipeline> logger) : IWatermarkPipeline
{
    public Task<IActionResult> ProcessAsync(
        Stream input,
        string originalFileName,
        WatermarkOptions options,
        DeliveryMode? modeOverride = null)
    {
        var ms = new MemoryStream();
        try
        {
            watermarker.Watermark(input, ms, options);
        }
        catch (Exception ex)
        {
            ms.Dispose();
            logger.LogError(ex, "Watermarking failed for file {Name}", originalFileName);
            IActionResult err = new ObjectResult(new { error = "Failed to process the PDF.", code = "PROCESSING_FAILED" })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return Task.FromResult(err);
        }

        // PdfStamper closes the stream on dispose; ToArray() is safe on a closed MemoryStream
        var fileName = Path.GetFileNameWithoutExtension(originalFileName) + "-watermarked.pdf";
        IActionResult result = new FileContentResult(ms.ToArray(), "application/pdf")
        {
            FileDownloadName = fileName
        };
        return Task.FromResult(result);
    }
}
