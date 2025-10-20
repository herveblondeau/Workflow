namespace Main.Recorders;

public class MultiSourceRecorder : IRecorder
{
    private readonly List<IRecorder> _sources;
    private MemoryStream _mixedStream = null!;
    private int _bitsPerSample;
    private int _sampleRate;

    public MultiSourceRecorder()
    {
        _sources = new();
    }

    public void Start(int sampleRate, int nbChannels, int bitsPerSample)
    {
        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;

        foreach (var source in _sources)
        {
            source.Start(sampleRate, nbChannels, bitsPerSample);
        }
    }

    public MultiSourceRecorder AddSource(IRecorder source)
    {
        _sources.Add(source);
        return this;
    }

    public async Task<Stream> Stop()
    {
        // Wait for all sources to stop recording
        await Task.WhenAll(_sources.Select(s => s.Stop()));

        // Mix the sources into a single combined stream
        int bytesPerSample = _bitsPerSample / 8;
        int bufferSize = _sampleRate * (_bitsPerSample / 8) * 1 / 10; // 100ms buffer
        var buffers = _sources.Select(r => new byte[bufferSize]).ToList();
        byte[] mixedBuffer = new byte[bufferSize];
        var bufferReaders = _sources.Select(r => r.GetOutputStream()).ToList();

        _mixedStream = new MemoryStream();
        while (true)
        {
            var bytes = bufferReaders.Select((br, i) => br.Read(buffers[i], 0, bufferSize)).ToList();
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

            _mixedStream.Write(mixedBuffer, 0, mixedBuffer.Length);
        }

        _mixedStream.Position = 0;
        return _mixedStream;
    }

    public Stream GetOutputStream()
    {
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
