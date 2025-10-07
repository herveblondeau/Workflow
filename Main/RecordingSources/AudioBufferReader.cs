using NAudio.Wave;

namespace Main.Recorders;

public class AudioBufferReader : IBufferReader
{
    private readonly MediaFoundationResampler _audioResampler = null!;

    public AudioBufferReader(MediaFoundationResampler audioResampler)
    {
        _audioResampler = audioResampler;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_audioResampler is null)
        {
            return 0; // No data to read
        }

        return _audioResampler.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        _audioResampler?.Dispose();
    }
}
