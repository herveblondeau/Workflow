using System.Diagnostics;

namespace Main.Recorders;

// IMPORTANT: This recorder captures system audio on Linux using FFmpeg and PulseAudio. Both tools must therefore be installed.
// - ffmpeg is most likely already installed and if not, is easily installable via package managers (e.g., apt, yum, pacman).
// - PulseAudio is the default sound server on many Linux distributions. If not installed, it can also be installed via package managers, in which case pulseaudio-utils is probably also necessary in order to run the 'pactl' command.
public class LinuxAudioRecorder : IRecorder
{
    private CancellationTokenSource _cts = null!;
    private Task _readTask = null!;
    private Process _ffmpeg = null!;
    private MemoryStream _audioStream = null!;

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {

        // Step 1: Get the default sink
        string defaultSink = RunCommand("pactl", "info | grep 'Default Sink:' | awk '{print $3}'").Trim();
        if (string.IsNullOrEmpty(defaultSink))
        {
            Console.WriteLine("Could not detect default sink.");
            return;
        }

        // Step 2: Convert sink name to monitor source
        string monitorSource = defaultSink + ".monitor";
        Console.WriteLine($"Detected monitor source: {monitorSource}");

        // Step 3: Build FFmpeg arguments to output raw PCM to stdout
        string ffmpegArgs = $"-f pulse -i {monitorSource} -ac {nbChannels} -ar {sampleRate} -sample_fmt s{bitsPerSample} -f wav -";

        Console.WriteLine("Starting recording. Press Ctrl+C to stop.");

        _ffmpeg = new Process();
        _ffmpeg.StartInfo.FileName = "ffmpeg";
        _ffmpeg.StartInfo.Arguments = ffmpegArgs;
        _ffmpeg.StartInfo.UseShellExecute = false;
        _ffmpeg.StartInfo.RedirectStandardOutput = true; // redirect stdout to read stream
        _ffmpeg.StartInfo.RedirectStandardError = true;  // redirect stderr to console
        _ffmpeg.StartInfo.CreateNoWindow = true;

        _ffmpeg.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };

        _ffmpeg.Start();
        _ffmpeg.BeginErrorReadLine();

        // Step 4: Read stdout in real-time into a MemoryStream
        _audioStream = new MemoryStream();
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

        // Console.WriteLine("Recording... Press ENTER to stop.");
        // Console.ReadLine();
    }

    static string RunCommand(string command, string arguments)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command} {arguments}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
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

        Console.WriteLine($"Recording stopped. Captured {_audioStream.Length} bytes of audio.");

        _audioStream.Position = 0;
        return _audioStream;
    }

    public Stream GetOutputStream()
    {
        _audioStream.Position = 0;
        return _audioStream;
    }

    // public IBufferReader GetBufferReader()
    // {
    //     throw new NotImplementedException();
    // }

    public void Dispose()
    {
        _audioStream?.Dispose();
    }

    // private class AudioBufferReader : IBufferReader
    // {
    //     private readonly MediaFoundationResampler _audioResampler = null!;

    //     public AudioBufferReader(MediaFoundationResampler audioResampler)
    //     {
    //         _audioResampler = audioResampler;
    //     }

    //     public int Read(byte[] buffer, int offset, int count)
    //     {
    //         if (_audioResampler is null)
    //         {
    //             return 0; // No data to read
    //         }

    //         return _audioResampler.Read(buffer, offset, count);
    //     }

    //     public void Dispose()
    //     {
    //         _audioResampler?.Dispose();
    //     }
    // }
}
