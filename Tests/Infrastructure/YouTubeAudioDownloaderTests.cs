using System.Text;
using AwesomeAssertions;
using Core.Models;
using FluentResults;
using Infrastructure.Downloaders;
using Infrastructure.Processes;
using NSubstitute;

namespace Tests.Infrastructure;

public class YouTubeAudioDownloaderTests
{
    private const string VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
    private static readonly AudioFormat _audioFormat = new(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);

    // Stands in for yt-dlp, which reports through the file it writes rather than through stdout
    private static IProcessRunner CreateRunnerWriting(string audio)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllText(OutputPath(call.ArgAt<IReadOnlyList<string>>(1)), audio);
                return Task.FromResult(Result.Ok(""));
            });
        return processRunner;
    }

    // yt-dlp can leave a partial download behind on the way out
    private static IProcessRunner CreateRunnerWritingThenFailing(string partialAudio)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllText(OutputPath(call.ArgAt<IReadOnlyList<string>>(1)), partialAudio);
                return Task.FromResult(Result.Fail<string>("ProcessRunner: yt-dlp exited with code 1"));
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

    private static string OutputPath(IReadOnlyList<string> arguments) => arguments[arguments.ToList().IndexOf("-o") + 1];

    private static string CapturedOutputPath(IProcessRunner processRunner) =>
        OutputPath((IReadOnlyList<string>)processRunner.ReceivedCalls().Single().GetArguments()[1]!);

    [Fact]
    public async Task Transform_ReturnsTheDownloadedAudio()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("audio bytes");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var audio = result.Value;
        using var reader = new StreamReader(audio, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("audio bytes");
    }

    // The tool names the executable on every call, so an injected runner cannot
    // redirect it at some other binary
    [Fact]
    public async Task Transform_AlwaysRunsTheYtDlpExecutable()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);
        await result.Value.DisposeAsync();

        // Assert
        await processRunner.Received(1).Run(
            "yt-dlp",
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_PassesTheExtractionFlagsAndAudioFormatAsSeparateArguments()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);
        await result.Value.DisposeAsync();

        // Assert — yt-dlp parses the postprocessor args itself, so they stay one entry
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments =>
                arguments.Contains("-x") &&
                arguments[arguments.ToList().IndexOf("--audio-format") + 1] == "wav" &&
                arguments[arguments.ToList().IndexOf("--postprocessor-args") + 1] == "-ar 16000 -ac 1 -sample_fmt s16"),
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
        var processRunner = CreateRunnerWriting("audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(awkwardUrl);
        await result.Value.DisposeAsync();

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
        var processRunner = CreateRunnerWriting("audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var result = await sut.Transform(VideoUrl, cancellationTokenSource.Token);
        await result.Value.DisposeAsync();

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
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("exited with code 1"));
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenNoAudioFileIsGenerated()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok(""));
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains(nameof(YouTubeAudioDownloader)));
    }

    // The caller reads the audio from the returned stream, so deleting the file before
    // it is done leaves it reading a path that no longer resolves
    [Fact]
    public async Task Transform_KeepsTheTemporaryFileUntilTheReturnedStreamIsDisposed()
    {
        // Arrange
        var processRunner = CreateRunnerWriting("audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        var result = await sut.Transform(VideoUrl);

        // Assert
        var audioFile = CapturedOutputPath(processRunner);
        File.Exists(audioFile).Should().BeTrue();

        await result.Value.DisposeAsync();
        File.Exists(audioFile).Should().BeFalse();
    }

    [Fact]
    public async Task Transform_DeletesTheTemporaryFileWhenYtDlpFails()
    {
        // Arrange
        var processRunner = CreateRunnerWritingThenFailing("partial audio");
        var sut = new YouTubeAudioDownloader(processRunner, _audioFormat);

        // Act
        await sut.Transform(VideoUrl);

        // Assert
        File.Exists(CapturedOutputPath(processRunner)).Should().BeFalse();
    }
}
