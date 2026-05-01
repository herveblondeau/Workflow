using Microsoft.Extensions.AI;

namespace Infrastructure.ChatAgents;

public interface IChatClientFactory
{
    IChatClient Create(string provider, string? model = null);
}
