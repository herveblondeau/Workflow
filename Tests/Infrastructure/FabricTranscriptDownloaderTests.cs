using AwesomeAssertions;
using Infrastructure.Downloaders;
using Infrastructure.Processes;

namespace Tests.Infrastructure;

public class FabricTranscriptDownloaderTests
{
    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly ProcessResult _result;
        public string? FileName { get; private set; }
        public IReadOnlyList<string>? Arguments { get; private set; }

        public StubProcessRunner(ProcessResult result) => _result = result;

        public Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            return Task.FromResult(_result);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Transform_ReturnsFailForNullOrWhitespaceUrl(string? url)
    {
        var sut = new FabricTranscriptDownloader("en", new StubProcessRunner(new ProcessResult(0, "irrelevant", "")));

        var result = await sut.Transform(url!);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenFabricExitsNonZero()
    {
        // The fallback contract: a non-zero exit must fail so FirstSuccessfulTool falls through.
        var sut = new FabricTranscriptDownloader("en", new StubProcessRunner(new ProcessResult(1, "", "no transcript available")));

        var result = await sut.Transform("https://www.youtube.com/watch?v=abc");

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("no transcript available"));
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenTranscriptIsEmpty()
    {
        var sut = new FabricTranscriptDownloader("en", new StubProcessRunner(new ProcessResult(0, "   \n  ", "")));

        var result = await sut.Transform("https://www.youtube.com/watch?v=abc");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ReturnsTrimmedTranscriptOnSuccess()
    {
        var sut = new FabricTranscriptDownloader("en", new StubProcessRunner(new ProcessResult(0, "  hello transcript  \n", "")));

        var result = await sut.Transform("https://www.youtube.com/watch?v=abc");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello transcript");
    }

    [Fact]
    public async Task Transform_PassesUrlLanguageAndTranscriptFlagToFabric()
    {
        var runner = new StubProcessRunner(new ProcessResult(0, "transcript", ""));
        var sut = new FabricTranscriptDownloader("pt", runner);

        await sut.Transform("https://www.youtube.com/watch?v=abc");

        runner.FileName.Should().Be("fabric");
        runner.Arguments.Should().Contain("-y");
        runner.Arguments.Should().Contain("https://www.youtube.com/watch?v=abc");
        runner.Arguments.Should().Contain("--transcript");
        runner.Arguments.Should().Contain("--yt-dlp-args=--sub-langs pt");
    }
}
