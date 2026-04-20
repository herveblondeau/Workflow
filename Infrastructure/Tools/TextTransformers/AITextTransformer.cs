using Core;
using Core.ChatAgents;
using FluentResults;

namespace Infrastructure.TextTransformers;

public class AITextTransformer : ITool<string, string>
{
    private readonly IChatAgent _chatAgent;
    private readonly string _language;

    private readonly IEnumerable<string> _instructions;

    public AITextTransformer(IChatAgent chatAgent, string language, IEnumerable<string> instructions)
    {
        _chatAgent = chatAgent;
        _language = language;

        _instructions = instructions;
    }

    public async Task<Result<string>> Transform(string input, CancellationToken cancellationToken = default)
    {
        _chatAgent.InitializeConversation();

        var prompt = $"I am going to give you some content in the language '{_language}'. Please process it according to the following instructions:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, _instructions.Select(l => $"- {l}"))
            + Environment.NewLine
            + Environment.NewLine
            + "Here is the content:"
            + Environment.NewLine
            + input;
        try
        {
            return Result.Ok(await _chatAgent.Prompt(prompt));
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(AITextTransformer)}: cannot send prompt").CausedBy(ex));
        }
    }
}
