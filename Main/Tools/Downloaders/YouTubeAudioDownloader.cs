using System.Diagnostics;
using Main.Extensions;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

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

        Stream? downloadedStream = null;
        Exception? innerException = null;
        try
        {
            downloadedStream = await _downloadViaYoutubeExplode(input.Url, input.TargetSampleRate, input.TargetNbChannels, input.TargetBitsPerSample);
        }
        catch (Exception ex)
        {
            innerException = ex;
        }

        try
        {
            innerException = null;
            downloadedStream = await _downloadViaYtDlp(input.Url, input.TargetSampleRate, input.TargetNbChannels, input.TargetBitsPerSample);
        }
        catch (Exception ex)
        {
            innerException = ex;
        }

        if (innerException is not null)
        {
            throw new Exception("Cannot download", innerException);
        }

        if (downloadedStream is null)
        {
            throw new Exception("No stream was downloaded");
        }

        // Resample to target format
        var resampledStream = await downloadedStream.ResampleToPcmAsync(input.TargetSampleRate, input.TargetNbChannels, input.TargetBitsPerSample);

        State = ToolState.Idle;

        return resampledStream;
    }

    private async Task<Stream> _downloadViaYoutubeExplode(string videoUrl, int targetSampleRate, int targetNbChannels, int targetBitsPerSample)
    {
        YoutubeClient _youtubeClient = new();

        // Download audio stream
        var video = await _youtubeClient.Videos.GetAsync(videoUrl);
        var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);
        var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        return await _youtubeClient.Videos.Streams.GetAsync(streamInfo);
    }

    private async Task<Stream> _downloadViaYtDlp(string videoUrl, int targetSampleRate, int targetNbChannels, int targetBitsPerSample)
    {
        // Create a temporary file for yt-dlp to write to
        var tempFile = $"{Path.GetTempFileName()}.mp3";

        // Build process info
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            // -f bestaudio: pick best available audio
            // -o: output file path
            Arguments = $"-x --audio-format mp3 --extractor-args \"youtube:player-client=android,web\" -o \"{tempFile}\" {videoUrl}",
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
        catch
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }
    }

    public void Dispose()
    {
    }

}

public record YouTubeAudioDownloaderParams(string Url, int TargetSampleRate, int TargetNbChannels, int TargetBitsPerSample);
