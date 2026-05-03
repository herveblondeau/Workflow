using Anthropic.SDK;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel;
using Infrastructure.ChatAgents.ChatClients;

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
            "anthropic" => CreateAnthropic(model),
            "openai" => CreateOpenAI(model),
            "gemini" => CreateGemini(model),
            "openrouter" => CreateOpenRouter(model),
            _ => throw new ArgumentException($"Unknown provider '{provider}'.", nameof(provider))
        };
    }

    private IChatClient CreateAnthropic(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            model = _configuration["ANTHROPIC_DEFAULT_MODEL"];
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Anthropic model is not configured.");
        }
        var apiKey = _configuration["ANTHROPIC_API_KEY"]
            ?? throw new InvalidOperationException("Anthropic API key is not configured.");
        return new AnthropicClient(new APIAuthentication(apiKey)).Messages
            .AsBuilder()
            .ConfigureOptions(o => { o.ModelId = model; o.MaxOutputTokens = 8096; })
            .Build();
    }

    private IChatClient CreateOpenAI(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            model = _configuration["OPENAI_DEFAULT_MODEL"];
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("OpenAI model is not configured.");
        }
        var apiKey = _configuration["OPENAI_API_KEY"]
            ?? throw new InvalidOperationException("OpenAI API key is not configured.");
        return new ChatClient(model, new ApiKeyCredential(apiKey)).AsIChatClient();
    }

    private IChatClient CreateGemini(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            model = _configuration["GEMINI_DEFAULT_MODEL"];
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Gemini model is not configured.");
        }
        var apiKey = _configuration["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");
        return new GeminiChatClient(new GeminiClientOptions { ApiKey = apiKey })
            .AsBuilder()
            .ConfigureOptions(o => o.ModelId = model)
            .Build();
    }

    private IChatClient CreateOpenRouter(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            model = _configuration["OPENROUTER_DEFAULT_MODEL"];
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("OpenRouter model is not configured.");
        }
        var apiKey = _configuration["OPENROUTER_API_KEY"]
            ?? throw new InvalidOperationException("OpenRouter API key is not configured.");
        var client = new OpenRouterChatClient(apiKey);
        client.UseModel(model);
        return client;
    }
}
