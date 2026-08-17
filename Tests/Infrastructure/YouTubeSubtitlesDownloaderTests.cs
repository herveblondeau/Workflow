using AwesomeAssertions;
using FluentResults;
using Infrastructure.Downloaders;
using Infrastructure.Processes;
using NSubstitute;

namespace Tests.Infrastructure;

public class YouTubeSubtitlesDownloaderTests
{
    private const string VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
    private const string Language = "en";

    // Stands in for yt-dlp, which reports through the file it writes rather than through stdout
    private static IProcessRunner CreateRunnerWriting(string subtitles, string language = Language)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllText($"{OutputPrefix(call.ArgAt<IReadOnlyList<string>>(1))}.{language}.vtt", subtitles);
                return Task.FromResult(Result.Ok(""));
            });
        return processRunner;
    }

    private static IProcessRunner CreateRunnerReturning(Result<string> result)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return processRunner;
    }

    private static string OutputPrefix(IReadOnlyList<string> arguments) => arguments[arguments.ToList().IndexOf("-o") + 1];

    private static string CapturedOutputPrefix(IProcessRunner processRunner) =>
        OutputPrefix((IReadOnlyList<string>)processRunner.ReceivedCalls().Single().GetArguments()[1]!);

    [Fact]
    public async Task Transform_ReturnsSubtitlesWithoutTimestampsOrCueNumbers()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("WEBVTT\n\n1\n00:00:01.000 --> 00:00:03.000\nWe're no strangers to love\n");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("WEBVTT We're no strangers to love");
    }

    // The tool names the executable on every call, so an injected runner cannot
    // redirect it at some other binary
    [Fact]
    public async Task Transform_AlwaysRunsTheYtDlpExecutable()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("subtitles");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        await processRunner.Received(1).Run(
            "yt-dlp",
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_PassesTheDownloadFlagsAndLanguageAsSeparateArguments()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("subtitles");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments =>
                arguments.Contains("--skip-download") &&
                arguments.Contains("--write-subs") &&
                arguments.ToList().IndexOf("--sub-langs") >= 0 &&
                arguments[arguments.ToList().IndexOf("--sub-langs") + 1] == Language),
            Arg.Is<string?>(standardInput => standardInput == null),
            Arg.Any<CancellationToken>());
    }

    // Interpolating the URL into one command line split it apart on the first space,
    // so a URL was never safe to pass through
    [Fact]
    public async Task Transform_PassesTheVideoUrlAsOneArgumentEvenWhenItContainsSpacesAndQuotes()
    {
        // Arrange
        const string awkwardUrl = "https://example.com/watch?v=a b \"c\"";
        var processRunner = CreateRunnerWriting("subtitles");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        await sut.Transform(awkwardUrl);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments => arguments.Count(argument => argument == awkwardUrl) == 1),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_ForwardsCancellationToken()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("subtitles");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);
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
    public async Task Transform_ReturnsFailWhenYtDlpFails()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Fail<string>("ProcessRunner: yt-dlp exited with code 1"));
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("exited with code 1"));
    }

    // yt-dlp exits cleanly for a video that simply has no subtitles in that language
    [Fact]
    public async Task Transform_ReturnsFailWhenNoSubtitlesFileIsGenerated()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok(""));
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains(nameof(YouTubeSubtitlesDownloader)));
    }

    [Fact]
    public async Task Transform_DeletesTheTemporaryFiles()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("subtitles");
        var sut = new YouTubeSubtitlesDownloader(processRunner, Language);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        var prefix = CapturedOutputPrefix(processRunner);
        File.Exists(prefix).Should().BeFalse();
        File.Exists($"{prefix}.{Language}.vtt").Should().BeFalse();
    }
}
