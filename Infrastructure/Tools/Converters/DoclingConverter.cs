using Core;
using FluentResults;
using Infrastructure.Processes;

namespace Infrastructure.Converters;

/// <summary>
/// Converts a local file path or URL to Markdown using the <c>docling</c> CLI
/// (https://docling-project.github.io/docling/).
/// Supported formats include PDF, DOCX, PPTX, XLSX, HTML, Markdown, images, and more.
/// Requires <c>docling</c> to be installed and accessible on PATH.
/// </summary>
public class DoclingConverter : ITool<string, string>
{
    private readonly IProcessRunner _processRunner;

    public DoclingConverter(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<Result<string>> Transform(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Result.Fail($"{nameof(DoclingConverter)}: source is null or empty");
        }

        var outputDir = Path.Combine(Path.GetTempPath(), $"docling-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            ProcessResult result;
            try
            {
                result = await _processRunner.Run(
                    "docling",
                    ["convert", source, "--to", "md", "--output", outputDir, "--quiet"],
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return Result.Fail(new Error($"{nameof(DoclingConverter)}: docling process failed to start").CausedBy(ex));
            }

            if (result.ExitCode != 0)
            {
                return Result.Fail($"{nameof(DoclingConverter)}: docling exited with code {result.ExitCode} ({result.StandardError.Trim()})");
            }

            var mdFiles = Directory.GetFiles(outputDir, "*.md");
            if (mdFiles.Length == 0)
            {
                return Result.Fail($"{nameof(DoclingConverter)}: docling produced no output file in {outputDir}");
            }

            var markdown = await File.ReadAllTextAsync(mdFiles[0], cancellationToken);

            if (string.IsNullOrWhiteSpace(markdown))
            {
                return Result.Fail($"{nameof(DoclingConverter)}: docling returned empty markdown");
            }

            return Result.Ok(markdown);
        }
        finally
        {
            try { Directory.Delete(outputDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
