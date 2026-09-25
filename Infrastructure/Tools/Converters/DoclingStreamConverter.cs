using Core;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Converters;

/// <summary>
/// Converts a <see cref="Stream"/> to Markdown using the <c>docling</c> CLI by writing it to
/// a temporary file and delegating to <see cref="DoclingConverter"/>.
/// The caller is responsible for supplying the correct <paramref name="fileExtension"/>
/// (e.g. <c>"pdf"</c>, <c>"docx"</c>, <c>"pptx"</c>) — docling selects its parsing backend
/// from the file extension, and streams carry no reliable format metadata.
/// </summary>
public class DoclingStreamConverter : ITool<Stream, string>
{
    private readonly DoclingConverter _inner;
    private readonly string _fileExtension;

    public DoclingStreamConverter(IProcessRunner processRunner, string fileExtension)
    {
        _inner = new DoclingConverter(processRunner);
        _fileExtension = fileExtension.TrimStart('.');
    }

    public async Task<Result<string>> Transform(Stream input, CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return Result.Fail($"{nameof(DoclingStreamConverter)}: input stream is null");
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{_fileExtension}");
        try
        {
            await using (var fs = File.Create(tempFile))
            {
                await input.CopyToAsync(fs, cancellationToken);
            }

            return await _inner.Transform(tempFile, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Fail(new Error($"{nameof(DoclingStreamConverter)}: failed to write stream to temp file").CausedBy(ex));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best-effort cleanup */ }
        }
    }
}
