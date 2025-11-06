using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Core.ChatAgents.OpenRouter;

public class OpenRouterChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private string _model = null!;

    public OpenRouterChatClient()
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri("https://openrouter.ai/api"),
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer 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");
    }

    public void UseModel(string model)
    {
        _model = model;
    }

    public void Dispose()
    {
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_model))
        {
            throw new InvalidOperationException("Model must be set before making a request.");
        }

        var payload = new
        {
            model = _model,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Text
            }),
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content, CancellationToken.None);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Cannot fulfill request: {response.StatusCode}"));
        }

        var openRouterResponse = JsonSerializer.Deserialize<OpenRouterResponse>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

        if (openRouterResponse is null || openRouterResponse.choices is null || !openRouterResponse.choices.Any())
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "No response from OpenRouter."));
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, openRouterResponse.choices.First().message.content));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        throw new NotImplementedException();
    }
}

public class OpenRouterResponse
{
    public string id { get; set; } = null!;
    public string provider { get; set; } = null!;
    public string model { get; set; } = null!;
    public string @object { get; set; } = null!;
    public int created { get; set; }
    public List<Choice> choices { get; set; } = null!;
    public object system_fingerprint { get; set; } = null!;
    public Usage usage { get; set; } = null!;

    public class Choice
    {
        public object logprobs { get; set; } = null!;
        public string finish_reason { get; set; } = null!;
        public string native_finish_reason { get; set; } = null!;
        public int index { get; set; }
        public Message message { get; set; } = null!;
    }

    public class CompletionTokensDetails
    {
        public int reasoning_tokens { get; set; }
    }

    public class Message
    {
        public string role { get; set; } = null!;
        public string content { get; set; } = null!;
        public object refusal { get; set; } = null!;
        public object reasoning { get; set; } = null!;
    }

    public class PromptTokensDetails
    {
        public int cached_tokens { get; set; }
    }

    public class Usage
    {
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public PromptTokensDetails prompt_tokens_details { get; set; } = null!;
        public CompletionTokensDetails completion_tokens_details { get; set; } = null!;
    }
}
