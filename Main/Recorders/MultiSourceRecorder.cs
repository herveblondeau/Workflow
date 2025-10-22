namespace Main.Recorders;

public class MultiSourceRecorder : IRecorder
{
    private readonly List<IRecorder> _sources;
    private MemoryStream _mixedStream = null!;
    private int _bitsPerSample;
    private int _sampleRate;
    public RecorderState State { get; private set; }

    public MultiSourceRecorder()
    {
        State = RecorderState.Stopped;
        _sources = new();
    }

    public MultiSourceRecorder AddSource(IRecorder source)
    {
        _sources.Add(source);
        return this;
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        State = RecorderState.Starting;

        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;

        foreach (var source in _sources)
        {
            source.Start(sampleRate, nbChannels, bitsPerSample);
        }

        _mixedStream = new MemoryStream();

        State = RecorderState.Recording;
    }

    public async Task Stop()
    {
        State = RecorderState.Stopping;

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

        State = RecorderState.Stopped;
    }

    public Stream? GetRecordedStream()
    {
        if (_mixedStream is null)
        {
            return null;
        }

        if (State != RecorderState.Stopped)
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
