using NAudio.Wave;
using System.Text;
using Whisper.net.Ggml;
using Whisper.net;
using Main.Recorders;

namespace Main.Recorders;

public class BufferedRecorder : IRecorder
{
    private readonly List<IRecordingSource> _sources;
    private readonly WaveFormat _format;

    public BufferedRecorder(WaveFormat format)
    {
        _sources = new List<IRecordingSource>();
        _format = format;
    }

    public async Task SetUp()
    {
        foreach (var source in _sources)
        {
            source.SetUp();
        }
    }

    public async Task Start()
    {
        foreach (var source in _sources)
        {
            source.StartRecording();
        }
    }

    public void AddSource(IRecordingSource source)
    {
        _sources.Add(source);
    }

    public async Task<byte[]> Stop()
    {
        foreach (var source in _sources)
        {
            source.StopRecording();
        }

        int bytesPerSample = _format.BitsPerSample / 8;
        int bufferSize = _format.AverageBytesPerSecond / 10; // 100ms buffer
        var buffers = _sources.Select(r => new byte[bufferSize]).ToList();
        byte[] mixedBuffer = new byte[bufferSize];
        var bufferReaders = _sources.Select(r => r.GetBufferReader());

        List<byte> output = new();
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

                BitConverter.GetBytes((short)mixed).CopyTo(mixedBuffer, i);
            }

            output.AddRange(mixedBuffer.Take(maxBytes));
        }

        foreach (var source in _sources)
        {
            source.Dispose();
        }

        return output.ToArray();
    }
}
