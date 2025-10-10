using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Recorders.MultiSource.RecordingSources;

public class AudioRecordingSource : IRecordingSource
{
    public RecorderStatus Status { get; private set; }
    private readonly WaveFormat _targetFormat;
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioBuffer = null!;
    private AudioBufferReader _audioBufferReader = null!;

    public AudioRecordingSource(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;
        Status = RecorderStatus.Initial;
    }

    public void SetUp()
    {
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _audioCapture = new WasapiLoopbackCapture(device);
        _audioBuffer = new MemoryStream();
        Console.WriteLine($"System audio format: {_audioCapture.WaveFormat}");
        _audioCapture.DataAvailable += (s, e) =>
        {
            if (e.BytesRecorded > 0)
            {
                // Console.WriteLine($"System audio received {e.BytesRecorded} bytes");
                _audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        Status = RecorderStatus.Ready;
    }

    public void StartRecording()
    {
        Status = RecorderStatus.Starting;

        _audioCapture.StartRecording();

        Status = RecorderStatus.Recording;
    }

    public void StopRecording()
    {
        Status = RecorderStatus.Stopping;

        _audioCapture.StopRecording();
        Thread.Sleep(100);
        _audioCapture.Dispose();
        _audioBuffer.Position = 0;

        _audioBufferReader = new AudioBufferReader(new MediaFoundationResampler(new RawSourceWaveStream(_audioBuffer, _audioCapture.WaveFormat), _targetFormat)
        {
            ResamplerQuality = 60
        });

        Status = RecorderStatus.Recorded;
    }

    public void Reset()
    {
        Status = RecorderStatus.Initial;
    }
    public IBufferReader GetBufferReader()
    {
        if (_audioBufferReader == null)
        {
            throw new InvalidOperationException("Microphone recording source has not been stopped yet.");
        }
        return _audioBufferReader;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _audioBufferReader.Read(buffer, offset, count);
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
