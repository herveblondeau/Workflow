using Core;
using Core.Models;
using Core.Recorders;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Recorders;

// IMPORTANT: This recorder captures system audio on Linux using FFmpeg and PulseAudio. Both tools must therefore be installed.
// - ffmpeg is most likely already installed and if not, is easily installable via package managers (e.g., apt, yum, pacman).
// - PulseAudio is the default sound server on many Linux distributions. If not installed, it can also be installed via package managers, in which case pulseaudio-utils is probably also necessary in order to run the 'pactl' command.
public class LinuxAudioRecorder : ITool<Unit, AudioStream>, IStreamRecorder
{
    private const string _ffmpegExecutable = "ffmpeg";
    private const string _pulseAudioControlExecutable = "pactl";

    private static readonly TimeSpan _defaultSinkLookupTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner _processRunner;
    private readonly AudioFormat _audioFormat;

    private IRunningProcess? _ffmpeg;
    private Task? _captureTask;
    private MemoryStream _audioStream = null!;

    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }

    // The timeout covers the sink lookup only: the capture itself runs for as long as
    // the recording does, and is ended through the process handle rather than timed out
    public LinuxAudioRecorder(AudioFormat audioFormat, TimeSpan? sinkLookupTimeout = null)
        : this(new ProcessRunner(sinkLookupTimeout ?? _defaultSinkLookupTimeout), audioFormat) { }

    public LinuxAudioRecorder(IProcessRunner processRunner, AudioFormat audioFormat)
    {
        _processRunner = processRunner;
        _audioFormat = audioFormat;
    }

    public async Task<Result<AudioStream>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        try
        {
            await Start(_audioFormat.SampleRate, _audioFormat.NbChannels, _audioFormat.BitsPerSample);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(LinuxAudioRecorder)}: cannot start recording").CausedBy(ex));
        }

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        try
        {
            await Stop();
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(LinuxAudioRecorder)}: cannot stop recording").CausedBy(ex));
        }

        Stream? stream = GetRecordedStream();
        if (stream is null)
        {
            return Result.Fail($"{nameof(LinuxAudioRecorder)}: recorded stream is unavailable");
        }

        return new AudioStream(stream);
    }

    public async Task Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        _audioStream = new MemoryStream();

        var monitorSource = $"{await _getDefaultSink()}.monitor";

        // Raw PCM on stdout, which the capture loop below reads for as long as the recording lasts
        var arguments = new List<string>
        {
            "-f", "pulse",
            "-i", monitorSource,
            "-ar", $"{sampleRate}",
            "-ac", $"{nbChannels}",
            "-sample_fmt", $"s{bitsPerSample}",
            "-f", "wav",
            "-"
        };

        var started = _processRunner.Start(_ffmpegExecutable, arguments);
        if (started.IsFailed)
        {
            throw new InvalidOperationException($"{nameof(LinuxAudioRecorder)}: cannot start ffmpeg ({_describe(started.Errors)})");
        }

        _ffmpeg = started.Value;
        _captureTask = _capture(_ffmpeg.StandardOutput);
    }

    public async Task Stop()
    {
        if (_ffmpeg is null)
        {
            return;
        }

        var ffmpeg = _ffmpeg;
        _ffmpeg = null;

        try
        {
            // Ending ffmpeg closes the pipe, which lets the capture loop drain what is still
            // buffered and finish on its own. Cancelling the read instead would cut it short
            var stopped = await ffmpeg.Stop();

            if (_captureTask is not null)
            {
                await _captureTask;
            }

            // ffmpeg giving up mid-recording would otherwise surface as silently truncated audio
            if (stopped.IsFailed)
            {
                throw new InvalidOperationException($"{nameof(LinuxAudioRecorder)}: ffmpeg failed during capture ({_describe(stopped.Errors)})");
            }
        }
        finally
        {
            await ffmpeg.DisposeAsync();
        }
    }

    public Stream? GetRecordedStream()
    {
        if (_audioStream is null)
        {
            return null;
        }

        _audioStream.Position = 0;
        return _audioStream;
    }

    public void Dispose()
    {
        // Disposing the handle kills the process before it yields, so the child is gone by
        // the time this returns even though reaping it finishes on its own afterwards.
        // AsTask so abandoning the result stays safe whatever backs the ValueTask
        _ = _ffmpeg?.DisposeAsync().AsTask();
        _ffmpeg = null;

        _audioStream?.Dispose();
    }

    private async Task<string> _getDefaultSink()
    {
        // get-default-sink prints the sink name on its own, so there is nothing to parse out
        // of a field label that PulseAudio would translate to the host's locale
        var result = await _processRunner.Run(_pulseAudioControlExecutable, ["get-default-sink"]);
        if (result.IsFailed)
        {
            throw new InvalidOperationException(
                $"{nameof(LinuxAudioRecorder)}: cannot read the default PulseAudio sink ({_describe(result.Errors)}). Ensure PulseAudio is installed and running.");
        }

        var defaultSink = result.Value.Trim();
        if (string.IsNullOrEmpty(defaultSink))
        {
            throw new InvalidOperationException(
                $"{nameof(LinuxAudioRecorder)}: PulseAudio reports no default sink. Ensure PulseAudio is installed and running.");
        }

        return defaultSink;
    }

    private async Task _capture(Stream output)
    {
        var buffer = new byte[4096];

        int bytesRead;
        while ((bytesRead = await output.ReadAsync(buffer)) > 0)
        {
            await _audioStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }
    }

    private static string _describe(IEnumerable<IError> errors) => string.Join("; ", errors.Select(error => error.Message));
}
