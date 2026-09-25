using AwesomeAssertions;
using Infrastructure.Converters;
using Infrastructure.Processes;

namespace Tests.Infrastructure;

public class DoclingStreamConverterTests
{
    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly Func<string, IReadOnlyList<string>, ProcessResult> _handler;
        public string? LastTempFile { get; private set; }

        public StubProcessRunner(ProcessResult result)
            : this((_, _) => result) { }

        public StubProcessRunner(Func<string, IReadOnlyList<string>, ProcessResult> handler)
            => _handler = handler;

        public Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            var argList = arguments.ToList();
            // The source path is the second argument (after "convert")
            var convertIndex = argList.IndexOf("convert");
            if (convertIndex >= 0 && convertIndex + 1 < argList.Count)
                LastTempFile = argList[convertIndex + 1];

            var outputIndex = argList.IndexOf("--output");
            if (outputIndex >= 0 && outputIndex + 1 < argList.Count)
            {
                var outputDir = argList[outputIndex + 1];
                File.WriteAllText(Path.Combine(outputDir, "document.md"), "# Result");
            }

            return Task.FromResult(_handler(fileName, arguments));
        }
    }

    [Fact]
    public async Task Transform_ReturnsFailForNullStream()
    {
        var sut = new DoclingStreamConverter(new StubProcessRunner(new ProcessResult(0, "", "")), "pdf");

        var result = await sut.Transform(null!);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_WritesTempFileWithCorrectExtension()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, "", ""));
        var sut = new DoclingStreamConverter(runner, "docx");

        await sut.Transform(new MemoryStream("content"u8.ToArray()));

        runner.LastTempFile.Should().NotBeNull();
        Path.GetExtension(runner.LastTempFile).Should().Be(".docx");
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData(".pdf")]
    public async Task Transform_NormalisesLeadingDotInExtension(string extension)
    {
        var runner = new StubProcessRunner(new ProcessResult(0, "", ""));
        var sut = new DoclingStreamConverter(runner, extension);

        await sut.Transform(new MemoryStream("content"u8.ToArray()));

        Path.GetExtension(runner.LastTempFile).Should().Be(".pdf");
    }

    [Fact]
    public async Task Transform_DeletesTempFileAfterSuccess()
    {
        string? tempFile = null;
        var runner = new StubProcessRunner((_, args) =>
        {
            var idx = args.ToList().IndexOf("convert");
            tempFile = args.ToList()[idx + 1];
            var outIdx = args.ToList().IndexOf("--output");
            File.WriteAllText(Path.Combine(args.ToList()[outIdx + 1], "doc.md"), "# ok");
            return new ProcessResult(0, "", "");
        });
        var sut = new DoclingStreamConverter(runner, "pdf");

        await sut.Transform(new MemoryStream("content"u8.ToArray()));

        tempFile.Should().NotBeNull();
        File.Exists(tempFile).Should().BeFalse();
    }

    [Fact]
    public async Task Transform_DeletesTempFileAfterFailure()
    {
        string? tempFile = null;
        var runner = new StubProcessRunner((_, args) =>
        {
            var idx = args.ToList().IndexOf("convert");
            tempFile = args.ToList()[idx + 1];
            return new ProcessResult(1, "", "bad format");
        });
        var sut = new DoclingStreamConverter(runner, "pdf");

        await sut.Transform(new MemoryStream("content"u8.ToArray()));

        tempFile.Should().NotBeNull();
        File.Exists(tempFile).Should().BeFalse();
    }

    [Fact]
    public async Task Transform_PropagatesDoclingFailure()
    {
        var sut = new DoclingStreamConverter(new StubProcessRunner(new ProcessResult(1, "", "unsupported")), "xyz");

        var result = await sut.Transform(new MemoryStream("content"u8.ToArray()));

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("unsupported"));
    }

    [Fact]
    public async Task Transform_PassesStreamBytesToTempFile()
    {
        byte[]? written = null;
        var runner = new StubProcessRunner((_, args) =>
        {
            var idx = args.ToList().IndexOf("convert");
            var path = args.ToList()[idx + 1];
            written = File.ReadAllBytes(path);
            var outIdx = args.ToList().IndexOf("--output");
            File.WriteAllText(Path.Combine(args.ToList()[outIdx + 1], "doc.md"), "# ok");
            return new ProcessResult(0, "", "");
        });
        var sut = new DoclingStreamConverter(runner, "pdf");
        var payload = "hello docling"u8.ToArray();

        await sut.Transform(new MemoryStream(payload));

        written.Should().Equal(payload);
    }
}
