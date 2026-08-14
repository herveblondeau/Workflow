using AwesomeAssertions;
using FluentResults;
using Infrastructure.Downloaders;
using Infrastructure.Processes;
using NSubstitute;

namespace Tests.Infrastructure;

public class FabricYouTubeTranscriptDownloaderTests
{
    private const string VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private static IProcessRunner CreateRunnerReturning(Result<string> result)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return processRunner;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Transform_ReturnsFailForNullOrWhitespaceUrlWithoutInvokingFabric(string? url)
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("unused"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        var result = await sut.Transform(url!);

        // Assert
        result.IsFailed.Should().BeTrue();
        await processRunner.DidNotReceive().Run(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_ReturnsTrimmedTranscript()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("  We're no strangers to love.\n"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("We're no strangers to love.");
    }

    // The tool names the executable on every call, so an injected runner cannot
    // redirect it at some other binary
    [Fact]
    public async Task Transform_AlwaysRunsTheFabricExecutable()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("transcript"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        await processRunner.Received(1).Run(
            "fabric",
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_PassesYoutubeFlagAndUrlAsSeparateArguments()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("transcript"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments =>
                arguments.Count == 2 &&
                arguments[0] == "-y" &&
                arguments[1] == VideoUrl),
            Arg.Is<string?>(standardInput => standardInput == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_AddsTimestampsFlagWhenRequested()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("transcript"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner, withTimestamps: true);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments => arguments.Contains("--transcript-with-timestamps")),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_ForwardsCancellationToken()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("transcript"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await sut.Transform(VideoUrl, cancellationTokenSource.Token);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            cancellationTokenSource.Token);
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenFabricFails()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Fail<string>("ProcessRunner: fabric exited with code 1"));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("exited with code 1"));
    }

    // A video without a transcript must fail so FirstSuccessfulTool falls through
    // to the subtitles/audio pipelines instead of forwarding an empty transcript
    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task Transform_ReturnsFailWhenTranscriptIsEmpty(string transcript)
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok(transcript));
        var sut = new FabricYouTubeTranscriptDownloader(processRunner);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains(nameof(FabricYouTubeTranscriptDownloader)));
    }
}
