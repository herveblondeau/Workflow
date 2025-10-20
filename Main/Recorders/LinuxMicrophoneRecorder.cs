using OpenTK.Audio.OpenAL;

namespace Main.Recorders;

public class LinuxMicrophoneRecorder : IRecorder
{
    private ALCaptureDevice _captureDevice;
    private MemoryStream _micStream = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        _micStream = new MemoryStream();

        string deviceName = ALC.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
        _captureDevice = ALC.CaptureOpenDevice(deviceName, sampleRate, _getALFormat(nbChannels, bitsPerSample), 4096);
        if (_captureDevice == IntPtr.Zero)
        {
            throw new Exception("Failed to open capture device");
        }

        _cancellationTokenSource = new();
        _ = _record();

        // Start capturing
        ALC.CaptureStart(_captureDevice);
    }

    private ALFormat _getALFormat(int nbChannels, int bitsPerSample)
    {
        if (nbChannels == 1)
        {
            return bitsPerSample == 8 ? ALFormat.Mono8 : ALFormat.Mono16;
        }
        else if (nbChannels == 2)
        {
            return bitsPerSample == 8 ? ALFormat.Stereo8 : ALFormat.Stereo16;
        }
        else
        {
            throw new NotSupportedException("Only mono and stereo are supported");
        }
    }

    public async Task<Stream> Stop()
    {
        ALC.CaptureStop(_captureDevice);
        ALC.CaptureCloseDevice(_captureDevice);

        _cancellationTokenSource.Cancel();

        await Task.Delay(100); // Give some time for the recording to stop

        return GetRecordedStream();
    }

    public Stream GetRecordedStream()
    {
        _micStream.Position = 0;
        return _micStream;
    }

    private async Task _record()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            byte[] samples = _readSamples();
            if (samples.Length > 0)
            {
                await _micStream.WriteAsync(samples, 0, samples.Length);
            }
            else
            {
                await Task.Delay(10);
            }
        }
    }

    private byte[] _readSamples()
    {
        // Check how many samples are available
        ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples, 10000, out int samplesAvailable);

        if (samplesAvailable > 0)
        {
            // Calculate buffer size based on format
            // int bytesPerSample = format == ALFormat.Mono16 ? 2 : 4;
            int bytesPerSample = 2;
            byte[] buffer = new byte[samplesAvailable * bytesPerSample];

            // Capture the samples
            ALC.CaptureSamples(_captureDevice, buffer, samplesAvailable);
            return buffer;
        }

        return Array.Empty<byte>();
    }

    public void Dispose()
    {
        _micStream?.Dispose();
    }
}
