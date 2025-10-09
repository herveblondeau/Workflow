using Microsoft.Extensions.AI;

namespace Main.ChatAgents.Models;

public class Conversation
{
    public Guid Id { get; private set; } = new();
    public List<ChatMessage> Messages { get; private set; } = new();

    public void AddSystemInput(string input)
    {
        Messages.Add(new(ChatRole.User, input));
    }

    public void AddUserInput(string input)
    {
        Messages.Add(new(ChatRole.User, input));
    }

    public void AddAssistantInput(string input)
    {
        Messages.Add(new(ChatRole.Assistant, input));
    }
}
