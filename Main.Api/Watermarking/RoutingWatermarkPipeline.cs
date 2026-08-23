using Infrastructure.Tools.Watermarking;
using Main.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Main.Api.Watermarking;

public class RoutingWatermarkPipeline(
    DiskWatermarkPipeline disk,
    StreamingWatermarkPipeline streaming,
    IConfiguration configuration) : IWatermarkPipeline
{
    public Task<IActionResult> ProcessAsync(
        Stream input,
        string originalFileName,
        WatermarkOptions options,
        DeliveryMode? modeOverride = null)
    {
        var mode = modeOverride
            ?? configuration.GetValue("Filigrane:DefaultDeliveryMode", DeliveryMode.Disk);

        return mode == DeliveryMode.Stream
            ? streaming.ProcessAsync(input, originalFileName, options)
            : disk.ProcessAsync(input, originalFileName, options);
    }
}
