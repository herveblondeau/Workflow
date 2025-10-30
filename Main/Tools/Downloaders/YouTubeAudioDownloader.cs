using System.Diagnostics;

namespace Main.Tools.Downloaders;

// Downloader that fetches audio from YouTube videos
// Requires yt-dlp to be installed and accessible in PATH
public class YouTubeAudioDownloader : ToolBase<YouTubeAudioDownloaderParams, Stream>, IDisposable
{
    public YouTubeAudioDownloader()
    {
        State = ToolState.Idle;
    }

    public override async Task<Stream> ProcessAsync(YouTubeAudioDownloaderParams input, CancellationToken cancellationToken = default)
    {
        State = ToolState.Running;

        Stream? downloadedStream = await _downloadViaYtDlp(input.Url, input.TargetSampleRate, input.TargetNbChannels, input.TargetBitsPerSample);
        if (downloadedStream is null)
        {
            throw new Exception("No stream was downloaded");
        }

        State = ToolState.Idle;

        return downloadedStream;
    }

    private async Task<Stream> _downloadViaYtDlp(string videoUrl, int targetSampleRate, int targetNbChannels, int targetBitsPerSample)
    {
        // Create a temporary file for yt-dlp to write to
        var tempFile = $"{Path.GetTempFileName()}.wav";

        // Build process info
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = $"-x --audio-format wav --extractor-args \"youtube:player-client=android,web\" {videoUrl} --postprocessor-args \"-ar {targetSampleRate} -ac {targetNbChannels} -sample_fmt s{targetBitsPerSample}\" -o \"{tempFile}\"",
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

            // Open file stream and let caller manage lifetime
            var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            return fileStream;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    public void Dispose()
    {
    }

}

public record YouTubeAudioDownloaderParams(string Url, int TargetSampleRate, int TargetNbChannels, int TargetBitsPerSample);
