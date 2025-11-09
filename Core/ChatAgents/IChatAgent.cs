namespace Core.ChatAgents;

public interface IChatAgent
{
    void InitializeConversation();
    Task<string> Prompt(string prompt, bool supplyHistory = true);
    Task<string> SetGuidelines(string guidelines);
}
