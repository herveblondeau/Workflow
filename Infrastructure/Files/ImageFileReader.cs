using Core;
using Core.Models;
using FileSignatures;
using FileSignatures.Formats;
using FluentResults;

namespace Infrastructure.Files;

public class ImageFileReader : ITool<string, ImageStream>
{
    public async Task<Result<ImageStream>> Transform(string filePath, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!File.Exists(filePath))
        {
            return Result.Fail<ImageStream>($"File not found: {filePath}");
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

        var inspector = new FileFormatInspector();
        var format = inspector.DetermineFileFormat(fileStream);
        if (format == null || format is not Image)
        {
            fileStream.Dispose();
            return Result.Fail<ImageStream>("The file is not a valid image.");
        }

        return Result.Ok(new ImageStream(fileStream));
    }
}
