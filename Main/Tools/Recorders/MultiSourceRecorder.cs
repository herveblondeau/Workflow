namespace Main.Tools.Recorders;

public class MultiSourceRecorder : ToolBase<RecorderParams, Stream>, IStreamRecorder
{
    private readonly List<IStreamRecorder> _sources;
    private MemoryStream _mixedStream = null!;
    private int _bitsPerSample;
    private int _sampleRate;
    public Func<CancellationToken, Task>? WaitForStopSignal { get; set; }

    public MultiSourceRecorder()
    {
        State = ToolState.Idle;
        _sources = new();
    }

    public override async Task<Stream> ProcessAsync(RecorderParams input, CancellationToken cancellationToken = default)
    {
        Start(input.SampleRate, input.NbChannels, input.BitsPerSample);

        if (WaitForStopSignal is not null)
        {
            await WaitForStopSignal.Invoke(cancellationToken);
        }

        await Stop();
        return GetRecordedStream()!;
    }


    public MultiSourceRecorder AddSource(IStreamRecorder source)
    {
        _sources.Add(source);
        return this;
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        State = ToolState.Starting;

        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;

        foreach (var source in _sources)
        {
            source.Start(sampleRate, nbChannels, bitsPerSample);
        }

        _mixedStream = new MemoryStream();

        State = ToolState.Running;
    }

    public async Task Stop()
    {
        State = ToolState.Stopping;

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

        State = ToolState.Idle;
    }

    public Stream? GetRecordedStream()
    {
        if (_mixedStream is null)
        {
            return null;
        }

        if (State != ToolState.Idle)
        {
            return null;
        }

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
