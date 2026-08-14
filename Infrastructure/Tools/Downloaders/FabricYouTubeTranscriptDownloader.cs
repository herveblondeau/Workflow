using Core;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Downloaders;

/// <summary>
/// Fetches the transcript of a YouTube video via `fabric -y`
/// Requires fabric (https://github.com/danielmiessler/Fabric) to be installed and accessible in PATH. It also needs to be configured (the config file is for instance located at ~/.config/fabric/.env on Linux)
/// </summary>
public class FabricYouTubeTranscriptDownloader : ITool<string, string>
{
    private const string _fabricExecutable = "fabric";

    private readonly IProcessRunner _processRunner;
    private readonly bool _withTimestamps;

    public FabricYouTubeTranscriptDownloader(bool withTimestamps = false, TimeSpan? timeout = null)
        : this(new ProcessRunner(timeout), withTimestamps) { }

    public FabricYouTubeTranscriptDownloader(IProcessRunner processRunner, bool withTimestamps = false)
    {
        _processRunner = processRunner;
        _withTimestamps = withTimestamps;
    }

    public async Task<Result<string>> Transform(string videoUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            return Result.Fail($"{nameof(FabricYouTubeTranscriptDownloader)}: video URL is required");
        }

        var arguments = new List<string> { "-y", videoUrl };
        if (_withTimestamps)
        {
            arguments.Add("--transcript-with-timestamps");
        }

        var result = await _processRunner.Run(_fabricExecutable, arguments, cancellationToken: cancellationToken);
        if (result.IsFailed)
        {
            return result;
        }

        // Videos without a transcript come back empty on a successful exit — failing here lets
        // FirstSuccessfulTool fall through to the subtitles/audio pipelines
        if (string.IsNullOrWhiteSpace(result.Value))
        {
            return Result.Fail($"{nameof(FabricYouTubeTranscriptDownloader)}: no transcript available for {videoUrl}");
        }

        return Result.Ok(result.Value.Trim());
    }
}
