using Core;
using Core.Models;
using Core.Recorders;
using FluentResults;

namespace Infrastructure.Recorders;

public class MultiSourceRecorder : ITool<Unit, AudioStream>, IStreamRecorder
{
    private readonly List<IStreamRecorder> _sources;
    private MemoryStream _mixedStream = null!;
    private int _bitsPerSample;
    private int _sampleRate;
    private Func<CancellationToken, Task>? _waitForStopSignal { get; set; }
    private readonly AudioFormat _audioFormat;

    public MultiSourceRecorder(AudioFormat audioFormat)
    {
        // State = ToolState.Idle;

        _sources = new();
        _audioFormat = audioFormat;
    }

    public MultiSourceRecorder AddSource(IStreamRecorder source)
    {
        _sources.Add(source);
        return this;
    }

    public MultiSourceRecorder AddStopSignal(Func<CancellationToken, Task> waitForStopSignal)
    {
        _waitForStopSignal = waitForStopSignal;
        return this;
    }

    public async Task<Result<AudioStream>> Transform(Unit _, CancellationToken cancellationToken = default)
    {
        try
        {
            Start(_audioFormat.SampleRate, _audioFormat.NbChannels, _audioFormat.BitsPerSample);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(MultiSourceRecorder)}: cannot start recording").CausedBy(ex));
        }

        if (_waitForStopSignal is not null)
        {
            await _waitForStopSignal.Invoke(cancellationToken);
        }

        try
        {
            await Stop();
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(MultiSourceRecorder)}: cannot stop recording").CausedBy(ex));
        }

        Stream? stream = GetRecordedStream();
        if (stream is null)
        {
            return Result.Fail($"{nameof(MultiSourceRecorder)}: recorded stream is unavailable");
        }

        return new AudioStream(stream);
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        // State = ToolState.Starting;

        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;

        foreach (var source in _sources)
        {
            source.Start(sampleRate, nbChannels, bitsPerSample);
        }

        _mixedStream = new MemoryStream();

        // State = ToolState.Running;
    }

    public async Task Stop()
    {
        // State = ToolState.Stopping;

        // Wait for all sources to stop recording
        await Task.WhenAll(_sources.Select(async s => await s.Stop()));

        // Mix the sources into a single combined stream
        int bytesPerSample = _bitsPerSample / 8;
        int bufferSize = _sampleRate * (_bitsPerSample / 8) * 1 / 10; // 100ms buffer
        var buffers = _sources.Select(r => new byte[bufferSize]).ToList();
        byte[] mixedBuffer = new byte[bufferSize];
        var streams = _sources.Select(r => r.GetRecordedStream()).ToList();

        while (true)
        {
            var bytes = streams.Select((s, i) => s!.Read(buffers[i], 0, bufferSize)).ToList();
            if (bytes.All(b => b == 0))
                break;

            int maxBytes = bytes.Max();

            for (int i = 0; i < maxBytes; i += bytesPerSample)
            {
                var samples = bytes.Select((_, n) => i < bytes[n] ? BitConverter.ToInt16(buffers[n], i) : (short)0).ToList();
                short mixed = 0;
                foreach (var sample in samples)
                {
                    mixed += sample;
                }
                mixed = Math.Clamp(mixed, short.MinValue, short.MaxValue);

                BitConverter.GetBytes(mixed).CopyTo(mixedBuffer, i);
            }

            _mixedStream!.Write(mixedBuffer, 0, mixedBuffer.Length);
        }

        // State = ToolState.Idle;
    }

    public Stream? GetRecordedStream()
    {
        if (_mixedStream is null)
        {
            return null;
        }

        // if (State != ToolState.Idle)
        // {
        //     return null;
        // }

        _mixedStream.Position = 0;
        return _mixedStream;
    }

    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _mixedStream?.Dispose();
    }
}
