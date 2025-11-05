using Main.ChatAgents;

namespace Main.Tools.TextTransformers;

public class AITextTransformer : ITool<string, string>
{
    private readonly ChatAgent _chatAgent;
    private readonly string _language;

    private readonly List<string> _instructions;

    public AITextTransformer(ChatAgent chatAgent, string language)
    {
        _chatAgent = chatAgent;
        _language = language;

        _instructions = new();
    }

    public AITextTransformer AddInstruction(string instruction)
    {
        _instructions.Add(instruction);
        return this;
    }

    public async Task<string> Transform(string input, CancellationToken cancellationToken = default)
    {
        _chatAgent.InitializeConversation();

        var prompt = @$"Here is some content in the language '{_language}': {input}
            Please transform it according to the following instructions:
            " + string.Join(Environment.NewLine, _instructions.Select(l => $"- {l}"));
        return await _chatAgent.Prompt(prompt);
    }
}
