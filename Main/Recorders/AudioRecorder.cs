using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Recorders;

public class AudioRecorder : IBufferableRecorder
{
    private WaveFormat _targetFormat = null!;
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioStream = null!;

    public void Start(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;

        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _audioStream = new MemoryStream();

        _audioCapture = new WasapiLoopbackCapture(device);
        _audioCapture.DataAvailable += _audioCapture_DataAvailable;
        _audioCapture.StartRecording();
    }

    private void _audioCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public async Task<Stream> Stop()
    {
        // Wait for the recording to stop
        // Note: calling StopRecording() only requests a stoppage. We have to query the object state to ensure it's actually stopped
        _audioCapture.StopRecording();
        while (_audioCapture.CaptureState != CaptureState.Stopped)
        {
            await Task.Delay(50);
        }

        // Clean up
        _audioCapture.DataAvailable -= _audioCapture_DataAvailable;
        _audioCapture.Dispose();

        // Resample the recorded stream to match the target format
        _audioStream.Position = 0;
        return _resample(_audioStream);
    }

    private Stream _resample(MemoryStream input)
    {
        var bufferReader = GetBufferReader();
        var resampledStream = new MemoryStream();
        int bytesPerSample = _targetFormat.BitsPerSample / 8;
        int bufferSize = _targetFormat.AverageBytesPerSecond / 10; // divide by 10 = 100ms buffer
        var resampledBuffer = new byte[bufferSize];
        while (true)
        {
            var nbBytes = bufferReader.Read(resampledBuffer, 0, bufferSize);
            if (nbBytes == 0)
                break;

            for (int i = 0; i < nbBytes; i += bytesPerSample)
            {
                var sample = i < nbBytes ? BitConverter.ToInt16(resampledBuffer, i) : (short)0;
                short mixed = sample;
                mixed = Math.Clamp(mixed, short.MinValue, short.MaxValue);

                BitConverter.GetBytes(mixed).CopyTo(resampledBuffer, i);
            }

            resampledStream.Write(resampledBuffer, 0, resampledBuffer.Length);
        }

        resampledStream.Position = 0;
        return resampledStream;
    }

    public IBufferReader GetBufferReader()
    {
        _audioStream.Position = 0;
        return new AudioBufferReader(new MediaFoundationResampler(new RawSourceWaveStream(_audioStream, _audioCapture.WaveFormat), _targetFormat)
        {
            ResamplerQuality = 60
        });
    }

    public void Dispose()
    {
        _audioCapture?.Dispose();
        _audioStream?.Dispose();
    }

    private class AudioBufferReader : IBufferReader
    {
        private readonly MediaFoundationResampler _audioResampler = null!;

        public AudioBufferReader(MediaFoundationResampler audioResampler)
        {
            _audioResampler = audioResampler;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_audioResampler is null)
            {
                return 0; // No data to read
            }

            return _audioResampler.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            _audioResampler?.Dispose();
        }
    }
}
