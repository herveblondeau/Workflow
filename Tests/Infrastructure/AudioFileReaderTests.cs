using System.Text;
using AwesomeAssertions;
using Infrastructure.Files;

namespace Tests.Infrastructure;

public class AudioFileReaderTests
{
    [Fact]
    public async Task Transform_ReturnsFailWhenFileDoesNotExist()
    {
        // Arrange
        var sut = new AudioFileReader();

        // Act
        var result = await sut.Transform("/nonexistent/path/audio.wav");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("File not found"));
    }

    [Fact]
    public async Task Transform_ReturnsFailForNonAudioFile()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "this is plain text, not audio");
            var sut = new AudioFileReader();

            // Act
            var result = await sut.Transform(path);

            // Assert
            result.IsFailed.Should().BeTrue();
            result.Errors.Should().Contain(e => e.Message.Contains("not a valid audio"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Transform_ReturnsAudioStreamForValidWavFile()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
        try
        {
            await File.WriteAllBytesAsync(path, BuildMinimalWav());
            var sut = new AudioFileReader();

            // Act
            var result = await sut.Transform(path);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildMinimalWav()
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int dataSize = 0;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write(bitsPerSample);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);

        return ms.ToArray();
    }
}
