using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.IO;

namespace Main.Recorders;

public class AudioRecorder : IBufferableRecorder
{
    private readonly WaveFormat _targetFormat;
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioBuffer = null!;
    private AudioBufferReader _audioBufferReader = null!;

    public AudioRecorder(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;
    }

    public Task SetUp()
    {
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _audioCapture = new WasapiLoopbackCapture(device);
        _audioBuffer = new MemoryStream();
        //Console.WriteLine($"System audio format: {_audioCapture.WaveFormat}");
        _audioCapture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded > 0)
            {
                // Console.WriteLine($"System audio received {e.BytesRecorded} bytes");
                _audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        return Task.CompletedTask;
    }

    public Task Start()
    {
        _audioCapture.StartRecording();

        return Task.CompletedTask;
    }

    public async Task<Stream> Stop()
    {
        _audioCapture.StopRecording();
        Thread.Sleep(100);
        _audioCapture.Dispose();
        _audioBuffer.Position = 0;

        await Task.CompletedTask;

        if (_targetFormat == _audioCapture.WaveFormat)
        {
            return _audioBuffer;
        }

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
        //return _audioBuffer;
        //var waveStream = new RawSourceWaveStream(_audioBuffer, _targetFormat);
        //var bla = new RawSourceWaveStream(_audioBuffer, _audioCapture.WaveFormat);
        //var bli = new MediaFoundationResampler(bla, _targetFormat);
        //var resampledStream = new WaveProviderToWaveStream(resampler);
        //return await _getResampledAudio(_audioBuffer, _targetFormat);
    }

    private async Task<Stream> _getResampledAudio(MemoryStream input, WaveFormat targetFormat)
    {
        input.Position = 0;
        await Task.Delay(1000);

        using (var reader = new WaveFileReader(input))
        {
            using (var resampler = new MediaFoundationResampler(reader, targetFormat))
            {
                var resampledStream = new MemoryStream();
                WaveFileWriter.WriteWavFileToStream(resampledStream, resampler);
                resampledStream.Position = 0;
                return resampledStream;
            }
        }
    }

    public IBufferReader GetBufferReader()
    {
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
