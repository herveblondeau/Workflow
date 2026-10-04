using System.Text;
using AwesomeAssertions;
using Infrastructure.Processes;
using Main.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Tests.Api;

public class AnalysisControllerMarkdownTests
{
    // Stub docling runner: mimics a successful `docling convert` by writing a markdown
    // file into the --output directory, exactly like the real CLI does.
    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly string _markdown;
        private readonly int _exitCode;
        private readonly string _stderr;

        public StubProcessRunner(string markdown = "# Converted", int exitCode = 0, string stderr = "")
        {
            _markdown = markdown;
            _exitCode = exitCode;
            _stderr = stderr;
        }

        public Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            if (_exitCode == 0)
            {
                var args = arguments.ToList();
                var outIdx = args.IndexOf("--output");
                if (outIdx >= 0 && outIdx + 1 < args.Count)
                    File.WriteAllText(Path.Combine(args[outIdx + 1], "document.md"), _markdown);
            }

            return Task.FromResult(new ProcessResult(_exitCode, "", _stderr));
        }
    }

    private static IFormFile FakeUpload(string fileName, byte[]? content = null)
    {
        var bytes = content ?? Encoding.UTF8.GetBytes("fake document bytes");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    private static AnalysisController CreateController(IProcessRunner runner)
        => new(chatClientFactory: null!, processRunner: runner);

    [Fact]
    public async Task TransformToMarkdown_ReturnsBadRequest_ForNullFile()
    {
        var controller = CreateController(new StubProcessRunner());

        var result = await controller.TransformToMarkdown(null!, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransformToMarkdown_ReturnsBadRequest_ForEmptyFile()
    {
        var controller = CreateController(new StubProcessRunner());
        var empty = new FormFile(new MemoryStream(Array.Empty<byte>()), 0, 0, "file", "empty.pdf");

        var result = await controller.TransformToMarkdown(empty, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransformToMarkdown_ReturnsBadRequest_WhenFileHasNoExtension()
    {
        var controller = CreateController(new StubProcessRunner());

        var result = await controller.TransformToMarkdown(FakeUpload("document"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransformToMarkdown_ReturnsOkWithMarkdown_OnSuccess()
    {
        var controller = CreateController(new StubProcessRunner(markdown: "# Hello from docling"));

        var result = await controller.TransformToMarkdown(FakeUpload("report.pdf"), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { success = true, result = "# Hello from docling" });
    }

    [Fact]
    public async Task TransformToMarkdown_Returns500_WhenDoclingFails()
    {
        var controller = CreateController(new StubProcessRunner(exitCode: 1, stderr: "unsupported format"));

        var result = await controller.TransformToMarkdown(FakeUpload("weird.xyz"), CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(500);
    }
}
