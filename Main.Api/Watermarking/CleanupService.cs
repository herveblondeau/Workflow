using Main.Api.Watermarking.Storage;

namespace Main.Api.Watermarking;

public class CleanupService(
    ITokenStore tokenStore,
    IFileStore fileStore,
    IConfiguration configuration,
    ILogger<CleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(
        configuration.GetValue<int>("Filigrane:CleanupIntervalMinutes", 30));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short initial delay to let the app finish starting, then sweep orphans
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        SweepOrphans();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);
            RunCleanup();
        }
    }

    private void RunCleanup()
    {
        var expired = tokenStore.GetExpired(DateTimeOffset.UtcNow).ToList();
        if (expired.Count == 0) return;

        logger.LogInformation("Cleanup: removing {Count} expired token(s)", expired.Count);
        foreach (var token in expired)
        {
            TryDeleteFile(token.FilePath);
            tokenStore.Remove(token.Token);
        }

        SweepOrphans();
    }

    private void SweepOrphans()
    {
        var knownFiles = tokenStore
            .GetAll()
            .Select(t => t.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in fileStore.GetAllFiles())
        {
            if (!knownFiles.Contains(file))
            {
                logger.LogWarning("Orphan file detected, deleting: {File}", file);
                TryDeleteFile(file);
            }
        }
    }

    private void TryDeleteFile(string path)
    {
        try { fileStore.Delete(path); }
        catch (Exception ex) { logger.LogError(ex, "Failed to delete file: {Path}", path); }
    }
}
