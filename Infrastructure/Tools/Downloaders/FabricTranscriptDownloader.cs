using Core;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Downloaders;

/// <summary>
/// Fetches a YouTube transcript via the fabric CLI (<c>fabric -y URL --transcript</c>).
/// Unlike <see cref="YouTubeSubtitlesDownloader"/> (which only retrieves manually-uploaded
/// subtitles), fabric also grabs auto-generated captions, so it succeeds on the majority of
/// videos and faster than the audio+Whisper fallback. On any failure it returns
/// <see cref="Result.Fail(string)"/> so a <c>FirstSuccessfulTool</c> can fall through.
/// </summary>
public class FabricTranscriptDownloader : ITool<string, string>
{
    private readonly string _language;
    private readonly IProcessRunner _processRunner;

    public FabricTranscriptDownloader(string language, IProcessRunner? processRunner = null)
    {
        _language = language;
        _processRunner = processRunner ?? new ProcessRunner();
    }

    public async Task<Result<string>> Transform(string videoUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            return Result.Fail($"{nameof(FabricTranscriptDownloader)}: video URL is null or empty");
        }

        // fabric's Go flag parser requires --yt-dlp-args=VALUE (single argv entry, '=' syntax);
        // this scopes yt-dlp to the requested subtitle language, mirroring YouTubeSubtitlesDownloader.
        var arguments = new[]
        {
            "-y", videoUrl,
            "--transcript",
            $"--yt-dlp-args=--sub-langs {_language}"
        };

        ProcessResult result;
        try
        {
            result = await _processRunner.Run("fabric", arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(FabricTranscriptDownloader)}: fabric process failed to start").CausedBy(ex));
        }

        if (result.ExitCode != 0)
        {
            return Result.Fail($"{nameof(FabricTranscriptDownloader)}: fabric exited with code {result.ExitCode} ({result.StandardError.Trim()})");
        }

        var transcript = result.StandardOutput.Trim();
        if (transcript.Length == 0)
        {
            return Result.Fail($"{nameof(FabricTranscriptDownloader)}: fabric returned an empty transcript");
        }

        return Result.Ok(transcript);
    }
}
