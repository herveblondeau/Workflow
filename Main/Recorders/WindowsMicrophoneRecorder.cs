using NAudio.Wave;

namespace Main.Recorders;

public class WindowsMicrophoneRecorder : IRecorder
{
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micStream = null!;
    public RecorderState State { get; private set; }

    public WindowsMicrophoneRecorder()
    {
        State = RecorderState.Stopped;
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        State = RecorderState.Starting;

        _micStream = new MemoryStream();

        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = new WaveFormat(sampleRate, bitsPerSample, nbChannels),
        };
        _micCapture.DataAvailable += _micCapture_DataAvailable;
        _micCapture.StartRecording();

        State = RecorderState.Recording;
    }

    private void _micCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        _micStream.Write(e.Buffer, 0, e.BytesRecorded);
    }

    public delegate void RecordingReadyHandler(object sender, StoppedEventArgs e);

    public async Task Stop()
    {
        State = RecorderState.Stopping;

        await _waitForRecordingStopped();

        _micCapture.DataAvailable -= _micCapture_DataAvailable;
        _micCapture.Dispose();

        State = RecorderState.Stopped;
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

    public Stream? GetRecordedStream()
    {
        if (_micStream is null)
        {
            return null!;
        }

        if (State != RecorderState.Stopped)
        {
            return null;
        }

        _micStream.Position = 0;
        return _micStream;
    }

    public void Dispose()
    {
        _micCapture?.Dispose();
        _micStream?.Dispose();
    }
}
