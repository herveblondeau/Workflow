using System.Diagnostics;
using System.Text.Json;

namespace Main.Extensions;

public static class StreamExtensions
{
    public static async Task<(int SampleRate, int NbChannels, int BitsPerSample)> GetAudioInfoAsync(this Stream inputStream)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = "-v quiet -print_format json -show_streams -", // "-" tells ffprobe to read from stdin
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start ffprobe process.");

        // Write the stream to ffprobe's stdin
        try
        {
            await inputStream.CopyToAsync(process.StandardInput.BaseStream);
            process.StandardInput.Close();
        }
        catch (IOException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            // FFprobe likely closed the pipe early — safe to ignore
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Same reason — FFprobe closed stdin early
        }

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var doc = JsonDocument.Parse(output);
        var stream = doc.RootElement.GetProperty("streams")[0];

        int sampleRate = stream.TryGetProperty("sample_rate", out var sr)
            ? int.Parse(sr.GetString()!)
            : 0;

        int nbChannels = stream.TryGetProperty("channels", out var ch)
            ? ch.GetInt32()
            : 0;

        int bitsPerSample = stream.TryGetProperty("bits_per_sample", out var bps)
            ? bps.GetInt32()
            : 0;

        return (sampleRate, nbChannels, bitsPerSample);
    }

    /// <summary>
    /// Resamples audio to PCM WAV format using FFmpeg.
    /// </summary>
    /// <param name="inputStream">Input audio stream.</param>
    /// <param name="targetSampleRate">Target sample rate (Hz), e.g., 16000.</param>
    /// <param name="channels">Number of output channels (1 = mono, 2 = stereo).</param>
    /// <param name="bitsPerSample">Bits per sample (8, 16, 24, or 32).</param>
    /// <returns>A Stream containing PCM WAV audio.</returns>
    public static async Task<Stream> ResampleToPcmAsync(
        this Stream inputStream,
        int targetSampleRate = 16000,
        int channels = 1,
        int bitsPerSample = 16)
    {
        if (inputStream == null)
            throw new ArgumentNullException(nameof(inputStream));

        if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
            throw new ArgumentOutOfRangeException(nameof(bitsPerSample), "Only 8, 16, 24, and 32 bits per sample are supported.");

        // Map bitsPerSample to ffmpeg codec name
        string codec = bitsPerSample switch
        {
            8 => "pcm_u8",     // unsigned 8-bit
            16 => "pcm_s16le", // signed 16-bit little-endian
            24 => "pcm_s24le", // signed 24-bit little-endian
            32 => "pcm_s32le", // signed 32-bit little-endian
            _ => throw new ArgumentException("Unsupported bits per sample.")
        };

        var outputStream = new MemoryStream();

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i - -f wav -ac {channels} -ar {targetSampleRate} -acodec {codec} -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        // Feed input to FFmpeg
        var inputTask = Task.Run(async () =>
        {
            try
            {
                await inputStream.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            }
            catch
            {
                // ignore broken pipe — ffmpeg may close stdin early
            }
        });

        // Capture FFmpeg stdout into our output stream
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream);

        // Optionally log FFmpeg errors
        var errorTask = Task.Run(async () =>
        {
            string err = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(err))
            {
                // Debug: Console.WriteLine(err);
            }
        });

        await Task.WhenAll(inputTask, outputTask, errorTask);
        await process.WaitForExitAsync();

        outputStream.Position = 0;
        return outputStream;
    }

    /// <summary>
    /// Resamples audio to 16-bit PCM WAV format using FFmpeg.
    /// </summary>
    /// <param name="inputStream">Input audio stream.</param>
    /// <param name="targetSampleRate">Desired sample rate (e.g., 16000 Hz).</param>
    /// <param name="channels">Number of channels (1=mono, 2=stereo). Default=1 (mono).</param>
    /// <returns>A stream containing 16-bit PCM WAV audio.</returns>
    public static async Task<Stream> ResampleAsync(this Stream inputStream, int targetSampleRate = 16000, int channels = 1)
    {
        if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));

        var outputStream = new MemoryStream();

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i - -f wav -ac {channels} -ar {targetSampleRate} -acodec pcm_s16le -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        // Pipe input stream to ffmpeg stdin
        var inputTask = Task.Run(async () =>
        {
            try
            {
                inputStream.Position = 0;
                await inputStream.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            }
            catch
            {
                // Ignore broken pipe if ffmpeg exits early
            }
        });

        // Pipe ffmpeg stdout to memory stream
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream);

        // Optionally, capture errors for debugging
        var errorTask = Task.Run(async () =>
        {
            string err = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(err))
            {
                // You can log FFmpeg errors here if needed
                // Console.WriteLine(err);
            }
        });

        await Task.WhenAll(inputTask, outputTask, errorTask);
        await process.WaitForExitAsync();

        outputStream.Position = 0; // rewind for reading
        return outputStream;
    }
}
