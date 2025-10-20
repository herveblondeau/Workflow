using System.Diagnostics;

namespace Main.Recorders;

// IMPORTANT: This recorder captures system audio on Linux using FFmpeg and PulseAudio. Both tools must therefore be installed.
// - ffmpeg is most likely already installed and if not, is easily installable via package managers (e.g., apt, yum, pacman).
// - PulseAudio is the default sound server on many Linux distributions. If not installed, it can also be installed via package managers, in which case pulseaudio-utils is probably also necessary in order to run the 'pactl' command.
// - bash is also required
public class LinuxAudioRecorder : IRecorder
{
    private CancellationTokenSource _cts = null!;
    private Task _readTask = null!;
    private Process _ffmpeg = null!;
    private MemoryStream _audioStream = null!;

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
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

        // _ffmpeg.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        // _ffmpeg.BeginErrorReadLine();

        // Read stdout in real-time into a MemoryStream
        _cts = new CancellationTokenSource();

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

        _ffmpeg.Start();
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

    public async Task<Stream> Stop()
    {
        // Stop reading and kill FFmpeg
        _cts.Cancel();
        if (!_ffmpeg.HasExited)
            _ffmpeg.Kill();

        await _readTask;

        return GetRecordedStream();
    }

    public Stream GetRecordedStream()
    {
        _audioStream.Position = 0;
        return _audioStream;
    }

    public void Dispose()
    {
        _audioStream?.Dispose();
    }
}
