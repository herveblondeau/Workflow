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

        string content = await _downloadViaYtDlp(_url, _sourceLanguage);
        if (string.IsNullOrEmpty(content))
        {
            throw new Exception("No stream was downloaded");
        }

        var cleanedContent = _cleanSubtitlesContent(content);

        // State = ToolState.Idle;

        return cleanedContent;
    }

    private async Task<string> _downloadViaYtDlp(string videoUrl, string language)
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
                throw new InvalidOperationException("yt-dlp failed to start.");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"yt-dlp failed: {error}");
            }

            if (!File.Exists(tempFile))
            {
                throw new Exception("yt-dlp did not produce the expected subtitle file.");
            }

            return File.ReadAllText($"{tempFilePrefix}.{language}.vtt");
        }
        finally
        {
            if (File.Exists(tempFilePrefix))
            {
                File.Delete(tempFilePrefix);
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
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
