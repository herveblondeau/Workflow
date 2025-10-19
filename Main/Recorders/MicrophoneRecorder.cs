using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Recorders;

public class MicrophoneRecorder : IBufferableRecorder
{
    private WaveFormat _targetFormat = null!;
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micStream = null!;

    public void Start(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;

        _micStream = new MemoryStream();

        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = _targetFormat,
        };
        _micCapture.DataAvailable += _micCapture_DataAvailable;
        _micCapture.StartRecording();
    }

    private void _micCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        _micStream.Write(e.Buffer, 0, e.BytesRecorded);
    }

    public delegate void RecordingReadyHandler(object sender, StoppedEventArgs e);

    public async Task<Stream> Stop()
    {
        // Wait for the recording to stop
        await _waitForRecordingStopped();

        // Clean up
        _micCapture.DataAvailable -= _micCapture_DataAvailable;
        _micCapture.Dispose();

        // Return the captured stream
        _micStream.Position = 0;
        return _micStream;
    }

    private Task _waitForRecordingStopped()
    {
        var tcs = new TaskCompletionSource();
        EventHandler<StoppedEventArgs> handler = null!;
        handler = (s, e) =>
        {
            _micCapture.DataAvailable -= _micCapture_DataAvailable;
            _micCapture.RecordingStopped -= handler;
            tcs.SetResult();
        };
        _micCapture.RecordingStopped += handler;
        _micCapture.StopRecording();
        return tcs.Task;
    }

    public IBufferReader GetBufferReader()
    {
        _micStream.Position = 0;
        return new MicrophoneBufferReader(new RawSourceWaveStream(_micStream, _targetFormat));
    }

    public void Dispose()
    {
        _micCapture?.Dispose();
        _micStream?.Dispose();
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
