using OpenTK.Audio.OpenAL;
using NAudio.Wave;

namespace Main.Recorders;

public class LinuxMicrophoneRecorder : IBufferableRecorder
{
    private ALCaptureDevice _captureDevice;
    private MemoryStream _micStream = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;

    // private WaveFormat _targetFormat = null!;
    // private WaveInEvent _micCapture = null!;

    public void Start(WaveFormat targetFormat)
    {
        _micStream = new MemoryStream();

        string deviceName = ALC.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
        _captureDevice = ALC.CaptureOpenDevice(deviceName, 16000, ALFormat.Mono16, 4096);
        if (_captureDevice == IntPtr.Zero)
        {
            throw new Exception("Failed to open capture device");
        }

        _cancellationTokenSource = new();
        _ = _record();

        // Start capturing
        ALC.CaptureStart(_captureDevice);


        // _targetFormat = targetFormat;

        // _micStream = new MemoryStream();

        // _micCapture = new WaveInEvent
        // {
        //     DeviceNumber = 0, // Default mic
        //     WaveFormat = _targetFormat,
        // };
        // _micCapture.DataAvailable += _micCapture_DataAvailable;
        // _micCapture.StartRecording();
    }

    public async Task<Stream> Stop()
    {
        ALC.CaptureStop(_captureDevice);
        ALC.CaptureCloseDevice(_captureDevice);

        _cancellationTokenSource.Cancel();

        await Task.Delay(100); // Give some time for the recording to stop

        return _micStream;

        // // Wait for the recording to stop
        // await _waitForRecordingStopped();

        // // Clean up
        // _micCapture.DataAvailable -= _micCapture_DataAvailable;
        // _micCapture.Dispose();

        // // Return the captured stream
        // _micStream.Position = 0;
        // return _micStream;
    }

    public async Task _record()
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
                // Thread.Sleep(10);
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

    public IBufferReader GetBufferReader()
    {
        throw new NotImplementedException();
        // _micStream.Position = 0;
        // return new MicrophoneBufferReader(new RawSourceWaveStream(_micStream, _targetFormat));
    }

    public void Dispose()
    {
        // _micCapture?.Dispose();
        // _micStream?.Dispose();
    }
}
