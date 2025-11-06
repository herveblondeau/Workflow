using System.Diagnostics;

namespace Main.Tools.Recorders;

// IMPORTANT: This recorder captures system audio on Linux using FFmpeg and PulseAudio. Both tools must therefore be installed.
// - ffmpeg is most likely already installed and if not, is easily installable via package managers (e.g., apt, yum, pacman).
// - PulseAudio is the default sound server on many Linux distributions. If not installed, it can also be installed via package managers, in which case pulseaudio-utils is probably also necessary in order to run the 'pactl' command.
// - bash is also required
public class LinuxAudioRecorder : ITool<Unit, Stream>, IStreamRecorder
{
    private CancellationTokenSource _cts = null!;
    private Task _readTask = null!;
    private Process _ffmpeg = null!;
    private MemoryStream _audioStream = null!;
    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }
    private readonly int _targetSampleRate;
    private readonly int _targetBitsPerSample;
    private readonly int _targetNbChannels;

    public LinuxAudioRecorder(int targetSampleRate, int targetBitsPerSample, int targetNbChannels)
    {
        // State = ToolState.Idle;

        _targetSampleRate = targetSampleRate;
        _targetBitsPerSample = targetBitsPerSample;
        _targetNbChannels = targetNbChannels;
    }

    public async Task<Stream> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        Start(_targetSampleRate, _targetNbChannels, _targetBitsPerSample);

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        await Stop();
        return GetRecordedStream()!;
    }

   public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        // State = ToolState.Starting;

        _audioStream = new MemoryStream();

        // Get the default sink
        string defaultSink = _runCommand("pactl", "info | grep 'Default Sink:' | awk '{print $3}'").Trim();
        if (string.IsNullOrEmpty(defaultSink))
        {
            throw new Exception("Failed to get default sink from PulseAudio. Ensure PulseAudio is installed and running."); // TODO: consider using a custom exception
        }
        string monitorSource = defaultSink + ".monitor";

        // Build FFmpeg arguments to output raw PCM to stdout
        _ffmpeg = new Process();
        _ffmpeg.StartInfo.FileName = "ffmpeg";
        _ffmpeg.StartInfo.Arguments = $"-f pulse -i {monitorSource} -ac {nbChannels} -ar {sampleRate} -sample_fmt s{bitsPerSample} -f wav -";
        _ffmpeg.StartInfo.UseShellExecute = false;
        _ffmpeg.StartInfo.RedirectStandardOutput = true; // redirect stdout to read stream
        _ffmpeg.StartInfo.RedirectStandardError = true;  // redirect stderr to console
        _ffmpeg.StartInfo.CreateNoWindow = true;

        // Read stdout in real-time into a MemoryStream
        _cts = new CancellationTokenSource();

        _ffmpeg.Start();

        _readTask = Task.Run(async () =>
        {
            byte[] buffer = new byte[4096];
            try
            {
                int bytesRead;
                while (!_cts.Token.IsCancellationRequested &&
                    (bytesRead = await _ffmpeg.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                {
                    _audioStream.Write(buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user stops recording; exit gracefully
            }
        }, _cts.Token);

        // State = ToolState.Running;
    }

    private static string _runCommand(string command, string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command} {arguments}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(processStartInfo)!)
        {
            return process.StandardOutput.ReadToEnd();
        }
    }

    public async Task Stop()
    {
        // State = ToolState.Stopping;

        // Stop reading and kill FFmpeg
        _cts.Cancel();
        if (!_ffmpeg.HasExited)
            _ffmpeg.Kill();

        await _readTask;

        // State = ToolState.Idle;
    }

    public Stream? GetRecordedStream()
    {
        if (_audioStream is null)
        {
            return null;
        }

        // if (State != ToolState.Idle)
        // {
        //     return null;
        // }

        _audioStream.Position = 0;
        return _audioStream;
    }

    public void Dispose()
    {
        _audioStream?.Dispose();
    }
}
