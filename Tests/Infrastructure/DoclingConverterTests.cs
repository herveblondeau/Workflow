using AwesomeAssertions;
using Infrastructure.Converters;
using Infrastructure.Processes;

namespace Tests.Infrastructure;

public class DoclingConverterTests
{
    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, ProcessResult> _handler;
        public string? FileName { get; private set; }
        public IReadOnlyList<string>? Arguments { get; private set; }

        public StubProcessRunner(ProcessResult result)
            : this((_, _) => result) { }

        public StubProcessRunner(Func<string, IReadOnlyList<string>, ProcessResult> handler)
            => _handler = handler;

        public Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            return Task.FromResult(_handler(fileName, arguments));
        }
    }

    // Creates a stub that writes a markdown file into the output directory
    // (simulating what docling would do) and returns exit code 0.
    private static StubProcessRunner SuccessfulRunner(string markdownContent = "# Hello") =>
        new((_, args) =>
        {
            // The output dir is the argument after "--output"
            var argList = args.ToList();
            var outputIndex = argList.IndexOf("--output");
            if (outputIndex >= 0 && outputIndex + 1 < argList.Count)
            {
                var outputDir = argList[outputIndex + 1];
                File.WriteAllText(Path.Combine(outputDir, "document.md"), markdownContent);
            }
            return new ProcessResult(0, "", "");
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Transform_ReturnsFailForNullOrWhitespaceSource(string? source)
    {
        var sut = new DoclingConverter(new StubProcessRunner(new ProcessResult(0, "", "")));

        var result = await sut.Transform(source!);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenDoclingExitsNonZero()
    {
        var sut = new DoclingConverter(new StubProcessRunner(new ProcessResult(1, "", "unsupported format")));

        var result = await sut.Transform("/tmp/some.pdf");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("unsupported format"));
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenNoOutputFileProduced()
    {
        // Exit 0 but write nothing to the output dir
        var sut = new DoclingConverter(new StubProcessRunner(new ProcessResult(0, "", "")));

        var result = await sut.Transform("/tmp/some.pdf");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("no output file"));
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenMarkdownIsEmpty()
    {
        var sut = new DoclingConverter(SuccessfulRunner("   \n  "));

        var result = await sut.Transform("/tmp/some.pdf");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("empty markdown"));
    }

    [Fact]
    public async Task Transform_ReturnsMarkdownOnSuccess()
    {
        const string expected = "# My Document\n\nSome content.";
        var sut = new DoclingConverter(SuccessfulRunner(expected));

        var result = await sut.Transform("/tmp/some.pdf");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Transform_PassesCorrectArgumentsToDocling()
    {
        var runner = SuccessfulRunner();
        var sut = new DoclingConverter(runner);

        await sut.Transform("/tmp/report.docx");

        runner.FileName.Should().Be("docling");
        runner.Arguments.Should().Contain("convert");
        runner.Arguments.Should().Contain("/tmp/report.docx");
        runner.Arguments.Should().Contain("--to");
        runner.Arguments.Should().Contain("md");
        runner.Arguments.Should().Contain("--output");
        runner.Arguments.Should().Contain("--quiet");
    }

    [Fact]
    public async Task Transform_StripsImagesToPlaceholders()
    {
        // docling's CLI defaults to embedding images as base64 data URIs; we force
        // placeholder mode so the markdown stays text-only.
        var runner = SuccessfulRunner();
        var sut = new DoclingConverter(runner);

        await sut.Transform("/tmp/report.docx");

        var args = runner.Arguments!.ToList();
        var idx = args.IndexOf("--image-export-mode");
        idx.Should().BeGreaterThanOrEqualTo(0);
        args[idx + 1].Should().Be("placeholder");
    }
}
