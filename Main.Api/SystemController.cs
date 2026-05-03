using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Main.Api.Models;
using Infrastructure.ChatAgents;

namespace Main.Api;

[Route("api/system")]
[ApiController]
public class SystemController : ControllerBase
{
    private readonly IEnumerable<IProviderModelSource> _modelSources;

    public SystemController(IEnumerable<IProviderModelSource> modelSources)
    {
        _modelSources = modelSources;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult GetStatus() => NoContent();

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_modelSources.Select(s => _fetchProvider(s, cancellationToken)));
        return Ok(results.Where(p => p != null));
    }

    private static async Task<ProviderInfo?> _fetchProvider(IProviderModelSource source, CancellationToken cancellationToken)
    {
        var models = await source.GetModelsAsync(cancellationToken);
        if (models is null)
        {
            return null;
        }

        return new ProviderInfo
        {
            Id = source.ProviderId,
            Label = source.ProviderLabel,
            Models = models.Select(m => new ModelInfo { Id = m.Id, Label = m.Label }).ToList()
        };
    }
}
