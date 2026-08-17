using Core;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Downloaders;

// Downloader that fetches subtitles from YouTube videos
// Requires yt-dlp to be installed and accessible in PATH
public class YouTubeSubtitlesDownloader : ITool<string, string>
{
    private const string _ytDlpExecutable = "yt-dlp";

    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(2);

    private readonly IProcessRunner _processRunner;
    private readonly string _language;

    public YouTubeSubtitlesDownloader(string language, TimeSpan? timeout = null)
        : this(new ProcessRunner(timeout ?? _defaultTimeout), language) { }

    public YouTubeSubtitlesDownloader(IProcessRunner processRunner, string language)
    {
        _processRunner = processRunner;
        _language = language;
    }

    public async Task<Result<string>> Transform(string videoUrl, CancellationToken cancellationToken = default)
    {
        var result = await _downloadViaYtDlp(videoUrl, _language, cancellationToken);
        if (result.IsFailed)
        {
            return result;
        }

        return _cleanSubtitlesContent(result.Value);
    }

    private async Task<Result<string>> _downloadViaYtDlp(string videoUrl, string language, CancellationToken cancellationToken)
    {
        // yt-dlp appends the language and container to the output template it is given
        var tempFilePrefix = Path.GetTempFileName();
        var tempFile = $"{tempFilePrefix}.{language}.vtt";

        try
        {
            var arguments = new List<string>
            {
                "--skip-download",
                "--write-subs",
                "--sub-langs", language,
                "-o", tempFilePrefix,
                videoUrl
            };

            var run = await _processRunner.Run(_ytDlpExecutable, arguments, cancellationToken: cancellationToken);
            if (run.IsFailed)
            {
                return run;
            }

            // yt-dlp exits cleanly for a video that has no subtitles in this language
            if (!File.Exists(tempFile))
            {
                return Result.Fail($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp subtitles file not generated");
            }

            try
            {
                return Result.Ok(await File.ReadAllTextAsync(tempFile, cancellationToken));
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp cannot read generated subtitles files ({tempFile})").CausedBy(ex));
            }
        }
        finally
        {
            _delete(tempFilePrefix);
            _delete(tempFile);
        }
    }

    private static void _delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Losing a temp file is not worth failing an otherwise successful download
        }
    }

    private string _cleanSubtitlesContent(string rawContent)
    {
        // Simple cleaning: remove timestamps and empty lines
        var lines = rawContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var cleanedLines = new List<string>();

        foreach (var line in lines)
        {
            // Skip lines that are timestamps
            if (line.Contains("-->") || int.TryParse(line, out _))
                continue;

            cleanedLines.Add(line.Trim());
        }

        return string.Join(" ", cleanedLines);
    }

    public void Dispose()
    {
    }
}
