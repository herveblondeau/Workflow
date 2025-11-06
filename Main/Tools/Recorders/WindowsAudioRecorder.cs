using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Main.Tools.Recorders;

public class WindowsAudioRecorder : ITool<Unit, Stream>, IStreamRecorder
{
    private WasapiLoopbackCapture _audioCapture = null!;
    private MemoryStream _audioStream = null!;
    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }
    private readonly int _targetSampleRate;
    private readonly int _targetBitsPerSample;
    private readonly int _targetNbChannels;

    public WindowsAudioRecorder(int targetSampleRate, int targetBitsPerSample, int targetNbChannels)
    {
        _targetSampleRate = targetSampleRate;
        _targetBitsPerSample = targetBitsPerSample;
        _targetNbChannels = targetNbChannels;
        // State = ToolState.Idle;
    }

    public async Task<Stream> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        Start(_targetSampleRate, _targetNbChannels, _targetBitsPerSample);

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        await Stop();
        return GetRecordedStream()!;
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        // State = ToolState.Starting;

        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _audioStream = new MemoryStream();

        _audioCapture = new WasapiLoopbackCapture(device);
        _audioCapture.DataAvailable += _audioCapture_DataAvailable;
        _audioCapture.WaveFormat = new WaveFormat(sampleRate, bitsPerSample, nbChannels);
        _audioCapture.StartRecording();

        // State = ToolState.Running;
    }

    private void _audioCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public async Task Stop()
    {
        // State = ToolState.Stopping;

        // Wait for the recording to stop
        // Note: calling StopRecording() only requests a stoppage. We have to query the object state to ensure it's actually stopped
        _audioCapture.StopRecording();
        while (_audioCapture.CaptureState != CaptureState.Stopped)
        {
            await Task.Delay(50);
        }

        // Clean up
        _audioCapture.DataAvailable -= _audioCapture_DataAvailable;
        _audioCapture.Dispose();

        // State = ToolState.Idle;
    }

    public Stream? GetRecordedStream()
    {
        if (_audioStream is null)
        {
            return null;
        }

        // if (State != ToolState.Idle)
        // {
        //     return null;
        // }

        _audioStream.Position = 0;
        return _audioStream;
    }

    public void Dispose()
    {
        _audioCapture?.Dispose();
        _audioStream?.Dispose();
    }
}
