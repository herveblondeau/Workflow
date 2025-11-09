using System.Diagnostics;
using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.Downloaders;

// Downloader that fetches audio from YouTube videos
// Requires yt-dlp to be installed and accessible in PATH
public class YouTubeAudioDownloader : ITool<Unit, Stream>
{
    private readonly string _url;
    private readonly AudioFormat _audioFormat;

    public YouTubeAudioDownloader(
        string url,
        AudioFormat audioFormat
    )
    {
        // State = ToolState.Idle;

        _url = url;
        _audioFormat = audioFormat;
    }

    public async Task<Result<Stream>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        // Create a temporary file for yt-dlp to write to
        var tempFile = $"{Path.GetTempFileName()}.wav";

        // Build process info
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = $"-x --audio-format wav --extractor-args \"youtube:player-client=android,web\" {_url} --postprocessor-args \"-ar {_audioFormat.SampleRate} -ac {_audioFormat.NbChannels} -sample_fmt s{_audioFormat.BitsPerSample}\" -o \"{tempFile}\"",
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
                return Result.Fail($"{nameof(YouTubeAudioDownloader)}: yt-dlp process failed to start");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return Result.Fail($"{nameof(YouTubeAudioDownloader)}: yt-dlp process exited with error ({error})");
            }

            // Open file stream and let caller manage lifetime
            Stream stream;
            try
            {
                 stream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"{nameof(YouTubeAudioDownloader)}: yt-dlp cannot open stream on file {tempFile}").CausedBy(ex));
            }
            return stream;
        }
        finally
        {
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
}
