using Infrastructure.Tools.Watermarking;
using Main.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Main.Api.Watermarking;

public interface IWatermarkPipeline
{
    Task<IActionResult> ProcessAsync(
        Stream input,
        string originalFileName,
        WatermarkOptions options,
        DeliveryMode? modeOverride = null);
}
