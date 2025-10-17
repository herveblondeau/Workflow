using NAudio.Wave;

namespace Main.Recorders;

public class MultiSourceRecorder : IRecorder
{
    private readonly List<IBufferableRecorder> _sources;
    private readonly WaveFormat _format;
    private MemoryStream _stream = null!;

    public MultiSourceRecorder(WaveFormat format)
    {
        _sources = new();
        _format = format;
    }

    public Task SetUp()
    {
        foreach (var source in _sources)
        {
            source.SetUp();
        }

        return Task.CompletedTask;
    }

    public async Task Start()
    {
        foreach (var source in _sources)
        {
            await source.Start();
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

        int bytesPerSample = _format.BitsPerSample / 8;
        int bufferSize = _format.AverageBytesPerSecond / 10; // 100ms buffer
        var buffers = _sources.Select(r => new byte[bufferSize]).ToList();
        byte[] mixedBuffer = new byte[bufferSize];
        var bufferReaders = _sources.Select(r => r.GetBufferReader());

        _stream = new MemoryStream();
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

            _stream.Write(mixedBuffer, 0, mixedBuffer.Length);
        }

        await Task.Delay(1000);

        _stream.Position = 0;
        return _stream;
    }

    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _stream.Dispose();
    }
}
