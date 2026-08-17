using System.Text;
using AwesomeAssertions;
using Core;
using Core.Models;
using FluentResults;
using Infrastructure.Processes;
using Infrastructure.Recorders;
using NSubstitute;

namespace Tests.Infrastructure;

public class LinuxAudioRecorderTests
{
    private const string DefaultSink = "alsa_output.pci-0000_00_1f.3.analog-stereo";

    private static readonly AudioFormat _audioFormat = new(SampleRate: 16000, BitsPerSample: 16, NbChannels: 1);

    private static IProcessRunner CreateRunner(
        Result<string>? defaultSink = null,
        StubRunningProcess? ffmpeg = null)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .Run(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defaultSink ?? Result.Ok($"{DefaultSink}\n")));
        processRunner
            .Start(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(Result.Ok<IRunningProcess>(ffmpeg ?? new StubRunningProcess("")));
        return processRunner;
    }

    // Recording ends on the stop signal, so a test has to supply one that fires straight away
    private static LinuxAudioRecorder CreateSut(IProcessRunner processRunner) =>
        new(processRunner, _audioFormat) { WaitForStopSignal = _ => Task.CompletedTask };

    [Fact]
    public async Task Transform_ReturnsWhatFfmpegWroteToStandardOutput()
    {
        // Arrange
        var ffmpeg = new StubRunningProcess("captured audio");
        var sut = CreateSut(CreateRunner(ffmpeg: ffmpeg));

        // Act
        var result = await sut.Transform(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var reader = new StreamReader(result.Value, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("captured audio");
    }

    // The lookup used to go through `bash -c "pactl info | grep … | awk …"`, which depended
    // on a shell and on the locale that labels the field. get-default-sink prints the name alone
    [Fact]
    public async Task Transform_LooksUpTheDefaultSinkThroughPactlWithoutAShell()
    {
        // Arrange
        var processRunner = CreateRunner();
        var sut = CreateSut(processRunner);

        // Act
        await sut.Transform(Unit.Value);

        // Assert
        await processRunner.Received(1).Run(
            "pactl",
            Arg.Is<IReadOnlyList<string>>(arguments => arguments.Count == 1 && arguments[0] == "get-default-sink"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // The tool names the executable itself, so an injected runner cannot redirect it
    [Fact]
    public async Task Transform_CapturesThroughTheFfmpegExecutable()
    {
        // Arrange
        var processRunner = CreateRunner();
        var sut = CreateSut(processRunner);

        // Act
        await sut.Transform(Unit.Value);

        // Assert
        processRunner.Received(1).Start("ffmpeg", Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Transform_CapturesFromTheDefaultSinkMonitorWithTheConfiguredAudioFormat()
    {
        // Arrange
        var processRunner = CreateRunner();
        var sut = CreateSut(processRunner);

        // Act
        await sut.Transform(Unit.Value);

        // Assert
        processRunner.Received(1).Start(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(arguments =>
                arguments[arguments.ToList().IndexOf("-i") + 1] == $"{DefaultSink}.monitor" &&
                arguments[arguments.ToList().IndexOf("-ar") + 1] == "16000" &&
                arguments[arguments.ToList().IndexOf("-ac") + 1] == "1" &&
                arguments[arguments.ToList().IndexOf("-sample_fmt") + 1] == "s16"));
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenTheDefaultSinkCannotBeLookedUp()
    {
        // Arrange
        var processRunner = CreateRunner(defaultSink: Result.Fail<string>("ProcessRunner: pactl exited with code 1"));
        var sut = CreateSut(processRunner);

        // Act
        var result = await sut.Transform(Unit.Value);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains(nameof(LinuxAudioRecorder)));
        processRunner.DidNotReceive().Start(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    // PulseAudio reports an empty name when no sink is configured, and exits cleanly doing so
    [Fact]
    public async Task Transform_ReturnsFailWhenThereIsNoDefaultSink()
    {
        // Arrange
        var processRunner = CreateRunner(defaultSink: Result.Ok("  \n"));
        var sut = CreateSut(processRunner);

        // Act
        var result = await sut.Transform(Unit.Value);

        // Assert
        result.IsFailed.Should().BeTrue();
        processRunner.DidNotReceive().Start(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Transform_StopsFfmpegWhenTheRecordingEnds()
    {
        // Arrange
        var ffmpeg = new StubRunningProcess("audio");
        var sut = CreateSut(CreateRunner(ffmpeg: ffmpeg));

        // Act
        await sut.Transform(Unit.Value);

        // Assert
        ffmpeg.StopCount.Should().Be(1);
    }

    // A recorder that was never stopped still holds a live child process
    [Fact]
    public async Task Dispose_StopsAFfmpegThatIsStillRunning()
    {
        // Arrange
        var ffmpeg = new StubRunningProcess("audio");
        var sut = new LinuxAudioRecorder(CreateRunner(ffmpeg: ffmpeg), _audioFormat);
        await sut.Start(_audioFormat.SampleRate, _audioFormat.NbChannels, _audioFormat.BitsPerSample);

        // Act
        sut.Dispose();

        // Assert
        ffmpeg.DisposeCount.Should().Be(1);
    }

    // Substitute.For cannot stand in here: the recorder reads StandardOutput to completion,
    // which needs a real stream rather than a recorded return value
    private sealed class StubRunningProcess(string output) : IRunningProcess
    {
        public Stream StandardOutput { get; } = new MemoryStream(Encoding.UTF8.GetBytes(output));

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task<Result> Stop(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.FromResult(Result.Ok());
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
