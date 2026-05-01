using Anthropic.SDK;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Infrastructure.ChatAgents.OpenRouter;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel;

namespace Infrastructure.ChatAgents;

public class ChatClientFactory : IChatClientFactory
{
    private readonly IConfiguration _configuration;

    public ChatClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IChatClient Create(string provider, string? model = null)
    {
        return provider.ToLowerInvariant() switch
        {
            "anthropic" => CreateAnthropic(model ?? "claude-sonnet-4-6"),
            "openai"    => CreateOpenAI(model ?? "gpt-4o-mini"),
            "gemini"    => CreateGemini(model ?? "gemini-2.0-flash"),
            "openrouter" => CreateOpenRouter(model ?? throw new ArgumentException("Model must be specified for OpenRouter.", nameof(model))),
            _ => throw new ArgumentException($"Unknown provider '{provider}'.", nameof(provider))
        };
    }

    private IChatClient CreateAnthropic(string model)
    {
        var apiKey = _configuration["ChatClients:Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic API key is not configured.");
        return new AnthropicClient(new APIAuthentication(apiKey)).Messages
            .AsBuilder()
            .ConfigureOptions(o => { o.ModelId = model; o.MaxOutputTokens = 8096; })
            .Build();
    }

    private IChatClient CreateOpenAI(string model)
    {
        var apiKey = _configuration["ChatClients:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key is not configured.");
        return new ChatClient(model, new ApiKeyCredential(apiKey)).AsIChatClient();
    }

    private IChatClient CreateGemini(string model)
    {
        var apiKey = _configuration["ChatClients:Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");
        return new GeminiChatClient(new GeminiClientOptions { ApiKey = apiKey })
            .AsBuilder()
            .ConfigureOptions(o => o.ModelId = model)
            .Build();
    }

    private IChatClient CreateOpenRouter(string model)
    {
        var apiKey = _configuration["ChatClients:OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("OpenRouter API key is not configured.");
        var client = new OpenRouterChatClient(apiKey);
        client.UseModel(model);
        return client;
    }
}
