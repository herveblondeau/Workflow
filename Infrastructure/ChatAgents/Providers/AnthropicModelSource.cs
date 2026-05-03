using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.ChatAgents.Providers;

public class AnthropicModelSource : IProviderModelSource
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderId => "anthropic";
    public string ProviderLabel => "Anthropic";

    public AnthropicModelSource(IConfiguration configuration) : this(configuration, new HttpClient()) { }

    public AnthropicModelSource(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<IList<ProviderModel>?> GetModelsAsync(CancellationToken cancellationToken)
    {
        var apiKey = _configuration["ANTHROPIC_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<Response>(json, _jsonOptions);
            return data?.Data?.Select(m => new ProviderModel(m.Id, m.Id)).ToList();
        }
        catch { return null; }
    }

    private record Response([property: JsonPropertyName("data")] List<Model>? Data);
    private record Model([property: JsonPropertyName("id")] string Id);
}
