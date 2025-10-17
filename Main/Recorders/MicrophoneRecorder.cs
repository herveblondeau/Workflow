using NAudio.Wave;

namespace Main.Recorders;

public class MicrophoneRecorder : IBufferableRecorder
{
    private readonly WaveFormat _targetFormat;
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micBuffer = null!;
    private MicrophoneBufferReader _micBufferReader = null!;

    public MicrophoneRecorder(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;
    }

    public Task SetUp()
    {
        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = _targetFormat,
        };
        _micBuffer = new MemoryStream();
        Console.WriteLine($"Mic format: {_micCapture.WaveFormat}");
        _micCapture.DataAvailable += (s, e) =>
        {
            _micBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        return Task.CompletedTask;
    }

    public Task Start()
    {
        _micCapture.StartRecording();
        return Task.CompletedTask;
    }

    public async Task<Stream> Stop()
    {
        _micCapture.StopRecording();
        Thread.Sleep(100);
        _micCapture.Dispose();
        _micBuffer.Position = 0;

        await Task.CompletedTask;
        return _micBuffer;
    }

    public IBufferReader GetBufferReader()
    {
        _micBufferReader = new MicrophoneBufferReader(new RawSourceWaveStream(_micBuffer, _targetFormat));
        return _micBufferReader;
    }

    public void Dispose()
    {
        _micCapture?.Dispose();
        _micBuffer?.Dispose();
        _micBufferReader?.Dispose();
    }

    private class MicrophoneBufferReader : IBufferReader
    {
        private readonly RawSourceWaveStream _micRaw = null!;

        public MicrophoneBufferReader(RawSourceWaveStream micRaw)
        {
            _micRaw = micRaw;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (_micRaw == null || _micRaw.Length == 0)
            {
                return 0; // No data to read
            }

            return _micRaw.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            _micRaw?.Dispose();
        }
    }
}
