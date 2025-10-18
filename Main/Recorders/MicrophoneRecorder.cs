using NAudio.Wave;

namespace Main.Recorders;

public class MicrophoneRecorder : IBufferableRecorder
{
    private WaveFormat _targetFormat = null!;
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micBuffer = null!;
    private MicrophoneBufferReader _micBufferReader = null!;

    public void Start(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;

        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = _targetFormat,
        };
        _micBuffer = new MemoryStream();
        _micCapture.DataAvailable += (s, e) =>
        {
            _micBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        _micCapture.StartRecording();
    }

    public Stream Stop()
    {
        _micCapture.StopRecording();
        Thread.Sleep(100);
        _micCapture.Dispose();

        _micBuffer.Position = 0;
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
