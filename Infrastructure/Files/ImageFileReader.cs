using Core;
using Core.Models;
using FileTypeChecker.Extensions;
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

        if (!fileStream.IsImage())
        {
            fileStream.Dispose();
            return Result.Fail<ImageStream>($"The file ({filePath}) is not a valid image.");
        }
        fileStream.Position = 0; // FileTypeChecker reads some bytes and doesn't reset the position; we must do it ourselves

        return Result.Ok(new ImageStream(fileStream));
    }
}
