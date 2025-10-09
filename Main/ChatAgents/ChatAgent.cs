using Microsoft.Extensions.AI;
using Main.ChatAgents.Models;

namespace Main.ChatAgents;

public class ChatAgent
{
    private readonly IChatClient _chatClient;
    private Conversation _conversation;

    public ChatAgent(IChatClient chatClient)
    {
        _chatClient = chatClient;
        _conversation = new();
    }

    public void InitializeConversation()
    {
        _conversation = new();
    }

    public async Task<string> SetGuidelines(string guidelines)
    {
        _conversation.AddSystemInput(guidelines);
        var response = await _chatClient.GetResponseAsync(_conversation.Messages);
        _conversation.AddAssistantInput(response.Text);
        return response.Text;
    }

    public async Task<string> Prompt(string prompt, bool supplyHistory = true)
    {
        ChatResponse response;
        if (supplyHistory)
        {
            _conversation.AddUserInput(prompt);
            response = await _chatClient.GetResponseAsync(_conversation.Messages);
            _conversation.AddAssistantInput(response.Text);
        }
        else
        {
            response = await _chatClient.GetResponseAsync(prompt);
        }
        return response.Text;
    }
}
