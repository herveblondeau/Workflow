using NAudio.Wave;

namespace Main.Recorders;

public class MultiSourceRecorder : IRecorder
{
    private readonly List<IBufferableRecorder> _sources;
    private WaveFormat _targetFormat = null!;
    private MemoryStream _mixedStream = null!;

    public MultiSourceRecorder()
    {
        _sources = new();
    }

    public void Start(WaveFormat targetFormat)
    {
        _targetFormat = targetFormat;

        foreach (var source in _sources)
        {
            source.Start(targetFormat);
        }
    }

    public MultiSourceRecorder AddSource(IBufferableRecorder source)
    {
        _sources.Add(source);
        return this;
    }

    public async Task<Stream> Stop()
    {
        foreach (var source in _sources)
        {
            await source.Stop();
        }

        int bytesPerSample = _targetFormat.BitsPerSample / 8;
        int bufferSize = _targetFormat.AverageBytesPerSecond / 10; // 100ms buffer
        var buffers = _sources.Select(r => new byte[bufferSize]).ToList();
        byte[] mixedBuffer = new byte[bufferSize];
        var bufferReaders = _sources.Select(r => r.GetBufferReader()).ToList();

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

        await Task.Delay(1000);
        _mixedStream.Position = 0;
        return _mixedStream;
    }

    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _mixedStream.Dispose();
    }
}
