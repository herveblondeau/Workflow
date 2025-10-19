using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Recorders;

public class AudioRecorder : IBufferableRecorder
{
    private WaveFormat _targetFormat = null!;
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioBuffer = null!;
    private AudioBufferReader _audioBufferReader = null!;

    public void Start(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;

        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _audioBuffer = new MemoryStream();

        _audioCapture = new WasapiLoopbackCapture(device);
        _audioCapture.DataAvailable += _audioCapture_DataAvailable;
        _audioCapture.StartRecording();
    }

    private void _audioCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            _audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public Stream Stop()
    {
        _audioCapture.StopRecording();
        _audioCapture.DataAvailable -= _audioCapture_DataAvailable;

        _audioBuffer.Position = 0;

        if (_targetFormat == _audioCapture.WaveFormat)
        {
            // TODO: this hasn't been tested so far because the capture uses PCM, which cannot be used to instantiate a WaveFormat
            return _audioBuffer;
        }
        else
        {
            return _resample(_audioBuffer);
        }
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
        _audioBuffer.Position = 0;
        _audioBufferReader = new AudioBufferReader(new MediaFoundationResampler(new RawSourceWaveStream(_audioBuffer, _audioCapture.WaveFormat), _targetFormat)
        {
            ResamplerQuality = 60
        });
        return _audioBufferReader;
    }

    public void Dispose()
    {
        _audioCapture?.Dispose();
        _audioBuffer?.Dispose();
        _audioBufferReader?.Dispose();
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
