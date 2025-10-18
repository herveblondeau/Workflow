using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Compression;
using System.IO;

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
        _audioCapture = new WasapiLoopbackCapture(device);
        _audioBuffer = new MemoryStream();
        _audioCapture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded > 0)
            {
                _audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        _audioCapture.StartRecording();
    }

    public async Task<Stream> Stop()
    {
        _audioCapture.StopRecording();
        Thread.Sleep(100);
        _audioCapture.Dispose();
        _audioBuffer.Position = 0;

        if (_targetFormat == _audioCapture.WaveFormat)
        {
            await Task.CompletedTask;
            return _audioBuffer;
        }
        else
        {
            await Task.CompletedTask;
            return _resample(_audioBuffer);
        }
    }

    private Stream _resample(MemoryStream input)
    {
        var bufferReader = GetBufferReader();
        var resampledStream = new MemoryStream();
        int bytesPerSample = _targetFormat.BitsPerSample / 8;
        int bufferSize = _targetFormat.AverageBytesPerSecond / 10; // 100ms buffer
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

    private class AudioBufferReader2 : IBufferReader
    {
        private readonly BufferedWaveProvider _bufferedWaveProvider = null!;

        public AudioBufferReader2(BufferedWaveProvider bufferedWaveProvider)
        {
            _bufferedWaveProvider = bufferedWaveProvider;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_bufferedWaveProvider is null)
            {
                return 0; // No data to read
            }

            return _bufferedWaveProvider.Read(buffer, offset, count);
        }

        public void Dispose()
        {
        }
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
