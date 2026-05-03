namespace Infrastructure.ChatAgents;

public interface IProviderModelSource
{
    string ProviderId { get; }
    string ProviderLabel { get; }
    Task<IList<ProviderModel>?> GetModelsAsync(CancellationToken cancellationToken);
}
