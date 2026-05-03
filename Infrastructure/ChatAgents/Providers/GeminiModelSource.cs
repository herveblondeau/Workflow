using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.ChatAgents.Providers;

public class GeminiModelSource : IProviderModelSource
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderId => "gemini";
    public string ProviderLabel => "Gemini";

    public GeminiModelSource(IConfiguration configuration) : this(configuration, new HttpClient()) { }

    public GeminiModelSource(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<IList<ProviderModel>?> GetModelsAsync(CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<Response>(json, _jsonOptions);
            return data?.Models?.Select(m => new ProviderModel(m.Name, m.DisplayName)).ToList();
        }
        catch { return null; }
    }

    private record Response([property: JsonPropertyName("models")] List<Model>? Models);
    private record Model(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("displayName")] string DisplayName);
}
