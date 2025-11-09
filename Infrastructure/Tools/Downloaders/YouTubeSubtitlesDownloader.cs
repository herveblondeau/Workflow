using System.Diagnostics;
using Core;
using FluentResults;

namespace Infrastructure.Downloaders;

// Downloader that fetches subtitles from YouTube videos
// Requires yt-dlp to be installed and accessible in PATH
public class YouTubeSubtitlesDownloader : ITool<Unit, string>
{
    private readonly string _url;
    private readonly string _sourceLanguage;

    public YouTubeSubtitlesDownloader(string url, string sourceLanguage)
    {
        // State = ToolState.Idle;

        _url = url;
        _sourceLanguage = sourceLanguage;
    }

    public async Task<Result<string>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        // State = ToolState.Running;

        var result = await _downloadViaYtDlp(_url, _sourceLanguage);
        if (result.IsFailed)
        {
            return result;
        }

        // State = ToolState.Idle;
        return _cleanSubtitlesContent(result.Value);
    }

    private async Task<Result<string>> _downloadViaYtDlp(string videoUrl, string language)
    {
        // Create a temporary file for yt-dlp to write to
        var tempFilePrefix = Path.GetTempFileName();
        var tempFile = $"{tempFilePrefix}.{language}.vtt";

        // Build process info
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = $"--skip-download --write-subs --sub-langs {language} -o \"{tempFilePrefix}\" {videoUrl}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                return Result.Fail($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp process failed to start");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return Result.Fail($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp process exited with error ({error})");
            }

            if (!File.Exists(tempFile))
            {
                return Result.Fail($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp subtitles file not generated");
            }

            string content;
            try
            {
                content = File.ReadAllText($"{tempFilePrefix}.{language}.vtt");
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"{nameof(YouTubeSubtitlesDownloader)}: yt-dlp cannot read generated subtitles files ({tempFilePrefix}.{language}.vtt)").CausedBy(ex));
            }

            return Result.Ok(content);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePrefix))
                {
                    File.Delete(tempFilePrefix);
                }
            }
            catch {}

            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch {}
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
