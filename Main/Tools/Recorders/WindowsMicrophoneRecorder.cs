using NAudio.Wave;

namespace Main.Tools.Recorders;

public class WindowsMicrophoneRecorder : ToolBase<WindowsMicrophoneRecorderParams, Stream>, IStreamRecorder
{
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micStream = null!;
    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }

    public WindowsMicrophoneRecorder()
    {
        State = ToolState.Idle;
    }

    public override async Task<Stream> ProcessAsync(WindowsMicrophoneRecorderParams input, CancellationToken cancellationToken = default)
    {
        Start(input.SampleRate, input.NbChannels, input.BitsPerSample);

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        await Stop();
        return GetRecordedStream()!;
    }


    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        State = ToolState.Starting;

        _micStream = new MemoryStream();

        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = new WaveFormat(sampleRate, bitsPerSample, nbChannels),
        };
        _micCapture.DataAvailable += _micCapture_DataAvailable;
        _micCapture.StartRecording();

        State = ToolState.Running;
    }

    private void _micCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        _micStream.Write(e.Buffer, 0, e.BytesRecorded);
    }

    public delegate void RecordingReadyHandler(object sender, StoppedEventArgs e);

    public async Task Stop()
    {
        State = ToolState.Stopping;

        await _waitForRecordingStopped();

        _micCapture.DataAvailable -= _micCapture_DataAvailable;
        _micCapture.Dispose();

        State = ToolState.Idle;
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

        if (State != ToolState.Idle)
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

public record WindowsMicrophoneRecorderParams(int SampleRate, int NbChannels, int BitsPerSample);
