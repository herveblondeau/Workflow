using NAudio.Wave;

namespace Main.Recorders;

public class MicrophoneRecorder : IRecorder
{
    public RecorderStatus Status { get; private set; }
    private readonly WaveFormat _targetFormat;
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micBuffer = null!;
    private MicrophoneBufferReader _micBufferReader = null!;

    public MicrophoneRecorder(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;
        Status = RecorderStatus.Initial;
    }

    public void SetUp()
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

        Status = RecorderStatus.Ready;
    }

    public void StartRecording()
    {
        Status = RecorderStatus.Starting;

        _micCapture.StartRecording();

        Status = RecorderStatus.Recording;
    }

    public void StopRecording()
    {
        Status = RecorderStatus.Stopping;

        _micCapture.StopRecording();
        Thread.Sleep(100);
        _micCapture.Dispose();
        _micBuffer.Position = 0;

        _micBufferReader = new MicrophoneBufferReader(new RawSourceWaveStream(_micBuffer, _targetFormat));

        Status = RecorderStatus.Recorded;
    }

    public void Reset()
    {
        Status = RecorderStatus.Initial;
    }
    public IBufferReader GetBufferReader()
    {
        if (_micBufferReader == null)
        {
            throw new InvalidOperationException("Microphone recording has not been stopped yet.");
        }
        return _micBufferReader;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _micBufferReader.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        _micCapture?.Dispose();
        _micBuffer?.Dispose();
        _micBufferReader?.Dispose();
    }
}
