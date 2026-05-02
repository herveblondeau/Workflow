using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Main.Api.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Main.Api;

[Route("api/system")]
[ApiController]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SystemController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult GetStatus() => NoContent();

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            GetAnthropicModels(cancellationToken),
            GetOpenAIModels(cancellationToken),
            GetGeminiModels(cancellationToken),
            GetOpenRouterModels(cancellationToken)
        );
        return Ok(results.Where(p => p != null));
    }

    private async Task<ProviderInfo?> GetAnthropicModels(CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = _configuration["ANTHROPIC_API_KEY"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var response = await client.GetAsync("https://api.anthropic.com/v1/models", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<AnthropicModelsResponse>(json, _jsonOptions);
            return new ProviderInfo
            {
                Id = "anthropic",
                Label = "anthropic",
                Models = data?.Data?.Select(m => new ModelInfo { Id = m.Id, Label = m.Id }).ToList() ?? []
            };
        }
        catch { return null; }
    }

    private async Task<ProviderInfo?> GetOpenAIModels(CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = _configuration["OPENAI_API_KEY"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync("https://api.openai.com/v1/models", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<OpenAIModelsResponse>(json, _jsonOptions);
            return new ProviderInfo
            {
                Id = "openai",
                Label = "openai",
                Models = data?.Data?.Select(m => new ModelInfo { Id = m.Id, Label = m.DisplayName ?? m.Id }).ToList() ?? []
            };
        }
        catch { return null; }
    }

    private async Task<ProviderInfo?> GetGeminiModels(CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = _configuration["GEMINI_API_KEY"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<GeminiModelsResponse>(json, _jsonOptions);
            return new ProviderInfo
            {
                Id = "gemini",
                Label = "gemini",
                Models = data?.Models?.Select(m => new ModelInfo { Id = m.Name, Label = m.DisplayName }).ToList() ?? []
            };
        }
        catch { return null; }
    }

    private async Task<ProviderInfo?> GetOpenRouterModels(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["OPENROUTER_API_KEY"];
            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync("https://openrouter.ai/api/v1/models", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<OpenRouterModelsResponse>(json, _jsonOptions);
            return new ProviderInfo
            {
                Id = "openrouter",
                Label = "openrouter",
                Models = data?.Data?.Select(m => new ModelInfo { Id = m.Id, Label = m.Name }).ToList() ?? []
            };
        }
        catch { return null; }
    }

    private record AnthropicModelsResponse(
        [property: JsonPropertyName("data")] List<AnthropicModel>? Data);

    private record AnthropicModel(
        [property: JsonPropertyName("id")] string Id);

    private record OpenAIModelsResponse(
        [property: JsonPropertyName("data")] List<OpenAIModel>? Data);

    private record OpenAIModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("display_name")] string? DisplayName);

    private record GeminiModelsResponse(
        [property: JsonPropertyName("models")] List<GeminiModel>? Models);

    private record GeminiModel(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("displayName")] string DisplayName);

    private record OpenRouterModelsResponse(
        [property: JsonPropertyName("data")] List<OpenRouterModel>? Data);

    private record OpenRouterModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);
}
