using System.Text;
using AwesomeAssertions;
using Core.Models;
using FluentResults;
using Infrastructure.Processes;
using Infrastructure.Tools.Transcribers;
using NSubstitute;

namespace Tests.Infrastructure;

public class TesseractOcrTranscriberTests
{
    private static IProcessRunner CreateRunnerReturning(Result<string> result)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return processRunner;
    }

    private static ImageStream CreateImage() => new(new MemoryStream(Encoding.UTF8.GetBytes("not really a PNG")));

    // The path tesseract was pointed at, so a test can check what became of that file
    private static string CapturedImagePath(IProcessRunner processRunner)
    {
        var arguments = (IReadOnlyList<string>)processRunner.ReceivedCalls().Single().GetArguments()[1]!;
        return arguments[0];
    }

    [Fact]
    public async Task Transform_ReturnsTheRecognisedText()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("recognised text"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");

        // Act
        var result = await sut.Transform(CreateImage());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("recognised text");
    }

    [Fact]
    public async Task Transform_ReturnsFailForUnsupportedLanguageWithoutInvokingTesseract()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("unused"));
        var sut = new TesseractOcrTranscriber(processRunner, "kl");

        // Act
        var result = await sut.Transform(CreateImage());

        // Assert
        result.IsFailed.Should().BeTrue();
        await processRunner.DidNotReceive().Run(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // The tool names the executable on every call, so an injected runner cannot
    // redirect it at some other binary
    [Fact]
    public async Task Transform_AlwaysRunsTheTesseractExecutable()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("text"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");

        // Act
        await sut.Transform(CreateImage());

        // Assert
        await processRunner.Received(1).Run(
            "tesseract",
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en", "eng")]
    [InlineData("fr", "fra")]
    [InlineData("ja", "jpn")]
    public async Task Transform_PassesTheImagePathStdoutMarkerAndLanguageAsSeparateArguments(string language, string tesseractCode)
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("text"));
        var sut = new TesseractOcrTranscriber(processRunner, language);

        // Act
        await sut.Transform(CreateImage());

        // Assert — "-" makes tesseract write to stdout instead of to a file
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments =>
                arguments.Count == 4 &&
                arguments[1] == "-" &&
                arguments[2] == "-l" &&
                arguments[3] == tesseractCode),
            Arg.Is<string?>(standardInput => standardInput == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_ForwardsCancellationToken()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("text"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await sut.Transform(CreateImage(), cancellationTokenSource.Token);

        // Assert
        await processRunner.Received(1).Run(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string?>(),
            cancellationTokenSource.Token);
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenTesseractFails()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Fail<string>("ProcessRunner: tesseract exited with code 1"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");

        // Act
        var result = await sut.Transform(CreateImage());

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("exited with code 1"));
    }

    // Tesseract needs the image on disk, so every run writes one out. Leaving those
    // behind fills the temp directory one OCR call at a time
    [Fact]
    public async Task Transform_DeletesTheTemporaryImageOnSuccess()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Ok("text"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");

        // Act
        await sut.Transform(CreateImage());

        // Assert
        File.Exists(CapturedImagePath(processRunner)).Should().BeFalse();
    }

    [Fact]
    public async Task Transform_DeletesTheTemporaryImageWhenTesseractFails()
    {
        // Arrange
        var processRunner = CreateRunnerReturning(Result.Fail<string>("ProcessRunner: tesseract exited with code 1"));
        var sut = new TesseractOcrTranscriber(processRunner, "en");

        // Act
        await sut.Transform(CreateImage());

        // Assert
        File.Exists(CapturedImagePath(processRunner)).Should().BeFalse();
    }
}
