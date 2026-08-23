using System.Collections.Concurrent;
using Main.Api.Filigrane.Models;

namespace Main.Api.Filigrane.Services;

public interface ITokenStore
{
    void Add(DownloadToken token);
    DownloadToken? Get(string token);
    bool TryConsume(string token, out DownloadToken? entry);
    IEnumerable<DownloadToken> GetExpired(DateTimeOffset cutoff);
    IEnumerable<DownloadToken> GetAll();
    void Remove(string token);
}

public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, DownloadToken> _store = new();

    public void Add(DownloadToken token) =>
        _store[token.Token] = token;

    public DownloadToken? Get(string token) =>
        _store.TryGetValue(token, out var entry) ? entry : null;

    public bool TryConsume(string token, out DownloadToken? entry)
    {
        entry = null;
        if (!_store.TryGetValue(token, out var existing))
            return false;

        // Atomic: only mark as downloaded if not already consumed
        lock (existing)
        {
            if (existing.Downloaded)
                return false;

            existing.Downloaded = true;
            entry = existing;
            return true;
        }
    }

    public IEnumerable<DownloadToken> GetExpired(DateTimeOffset cutoff) =>
        _store.Values.Where(t => t.ExpiresAt <= cutoff).ToList();

    public IEnumerable<DownloadToken> GetAll() =>
        _store.Values.ToList();

    public void Remove(string token) =>
        _store.TryRemove(token, out _);
}
