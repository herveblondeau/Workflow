using Core;
using Core.Models;
using Core.Recorders;
using FluentResults;
using NAudio.Wave;

namespace Infrastructure.Recorders;

public class WindowsMicrophoneRecorder : ITool<Unit, Stream>, IStreamRecorder
{
    private WaveInEvent _micCapture = null!;
    private MemoryStream _micStream = null!;
    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }
    private readonly AudioFormat _audioFormat;

    public WindowsMicrophoneRecorder(AudioFormat audioFormat)
    {
        // State = ToolState.Idle;

        _audioFormat = audioFormat;
    }

    public async Task<Result<Stream>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        try
        {
            Start(_audioFormat.SampleRate, _audioFormat.NbChannels, _audioFormat.BitsPerSample);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(WindowsMicrophoneRecorder)}: cannot start recording").CausedBy(ex));
        }

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        try
        {
            await Stop();
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(WindowsMicrophoneRecorder)}: cannot stop recording").CausedBy(ex));
        }

        Stream? stream = GetRecordedStream();
        if (stream is null)
        {
            return Result.Fail($"{nameof(WindowsMicrophoneRecorder)}: recorded stream is unavailable");
        }

        return stream;
    }


    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        // State = ToolState.Starting;

        _micStream = new MemoryStream();

        _micCapture = new WaveInEvent
        {
            DeviceNumber = 0, // Default mic
            WaveFormat = new WaveFormat(sampleRate, bitsPerSample, nbChannels),
        };
        _micCapture.DataAvailable += _micCapture_DataAvailable;
        _micCapture.StartRecording();

        // State = ToolState.Running;
    }

    private void _micCapture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        _micStream.Write(e.Buffer, 0, e.BytesRecorded);
    }

    public delegate void RecordingReadyHandler(object sender, StoppedEventArgs e);

    public async Task Stop()
    {
        // State = ToolState.Stopping;

        await _waitForRecordingStopped();

        _micCapture.DataAvailable -= _micCapture_DataAvailable;
        _micCapture.Dispose();

        // State = ToolState.Idle;
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

        // if (State != ToolState.Idle)
        // {
        //     return null;
        // }

        _micStream.Position = 0;
        return _micStream;
    }

    public void Dispose()
    {
        _micCapture?.Dispose();
        _micStream?.Dispose();
    }
}
