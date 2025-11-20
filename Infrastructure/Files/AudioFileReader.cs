using Core;
using Core.Models;
using FileTypeChecker;
using FileTypeChecker.Extensions;
using FluentResults;

namespace Infrastructure.Files;

public class AudioFileReader : ITool<string, AudioStream>
{
    public async Task<Result<AudioStream>> Transform(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!File.Exists(filePath))
        {
            return Result.Fail<AudioStream>($"File not found: {filePath}");
        }

        Stream fileStream;
        try
        {
            fileStream = File.OpenRead(filePath);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"Failed to open file: {ex.Message}").CausedBy(ex));
        }

        if (!fileStream.IsAudio())
        {
            fileStream.Dispose();
            return Result.Fail<AudioStream>($"The file ({filePath}) is not a valid audio.");
        }
        fileStream.Position = 0; // FileTypeChecker reads some bytes and doesn't reset the position; we must do it ourselves

        return Result.Ok(new AudioStream(fileStream));
    }
}

public static class StreamExtensions
{
    public static bool IsAudio(this Stream stream)
    {
        if (!FileTypeValidator.IsTypeRecognizable(stream))
        {
            return false;
        }

        var fileType = FileTypeValidator.GetFileType(stream);
        return
            fileType is FileTypeChecker.Types.Mp3
                || fileType is FileTypeChecker.Types.MpegAudio
                || fileType is FileTypeChecker.Types.WaveformAudioFileFormat
                || fileType is FileTypeChecker.Types.WindowsAudio;
    }
}