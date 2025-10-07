using NAudio.Wave;

namespace Main.Recorders;

public class MicrophoneBufferReader : IBufferReader
{
    private readonly RawSourceWaveStream _micRaw = null!;

    public MicrophoneBufferReader(RawSourceWaveStream micRaw)
    {
        _micRaw = micRaw;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_micRaw == null || _micRaw.Length == 0)
        {
            return 0; // No data to read
        }

        return _micRaw.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        _micRaw?.Dispose();
    }
}
