using Core;
using Core.Models;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Downloaders;

// Downloader that fetches audio from YouTube videos
// Requires yt-dlp and ffmpeg to be installed and accessible in PATH
public class YouTubeAudioDownloader : ITool<string, AudioStream>
{
    private const string _ytDlpExecutable = "yt-dlp";

    // Extracting and re-encoding the audio of a long video takes minutes, so the wait
    // is bounded well above what a download needs rather than at the runner's default
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(15);

    private readonly IProcessRunner _processRunner;
    private readonly AudioFormat _audioFormat;

    public YouTubeAudioDownloader(AudioFormat audioFormat, TimeSpan? timeout = null)
        : this(new ProcessRunner(timeout ?? _defaultTimeout), audioFormat) { }

    public YouTubeAudioDownloader(IProcessRunner processRunner, AudioFormat audioFormat)
    {
        _processRunner = processRunner;
        _audioFormat = audioFormat;
    }

    public async Task<Result<AudioStream>> Transform(string videoUrl, CancellationToken cancellationToken = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{nameof(YouTubeAudioDownloader)}-{Guid.NewGuid():N}.wav");

        var arguments = new List<string>
        {
            "-x",
            "--audio-format", "wav",
            "--extractor-args", "youtube:player-client=android,web",
            "--postprocessor-args", $"-ar {_audioFormat.SampleRate} -ac {_audioFormat.NbChannels} -sample_fmt s{_audioFormat.BitsPerSample}",
            "-o", tempFile,
            videoUrl
        };

        var run = await _processRunner.Run(_ytDlpExecutable, arguments, cancellationToken: cancellationToken);
        if (run.IsFailed)
        {
            _delete(tempFile); // yt-dlp may have left a partial download behind
            return run.ToResult();
        }

        if (!File.Exists(tempFile))
        {
            return Result.Fail($"{nameof(YouTubeAudioDownloader)}: yt-dlp audio file not generated");
        }

        try
        {
            // DeleteOnClose ties the file's lifetime to the stream, so the caller reads it
            // for as long as it needs and nothing is left behind once it disposes it
            var stream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            return new AudioStream(stream);
        }
        catch (Exception ex)
        {
            _delete(tempFile);
            return Result.Fail(new Error($"{nameof(YouTubeAudioDownloader)}: yt-dlp cannot open stream on file {tempFile}").CausedBy(ex));
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
            // Losing a temp file is not worth failing on top of whatever already went wrong
        }
    }
}
