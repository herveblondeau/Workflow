using AwesomeAssertions;
using Infrastructure.Processes;

namespace Tests.Infrastructure;

// Driven with POSIX utilities so the tests stay deterministic and need no network access.
// These use absolute /bin paths and are therefore Linux-only, consistent with the
// platform-specific recorders already in Infrastructure.
public class ProcessRunnerTests
{
    [Fact]
    public async Task Run_ReturnsStandardOutput()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act
        var result = await sut.Run("/bin/sh", ["-c", "printf hello"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task Run_WritesStandardInputAndClosesIt()
    {
        // Arrange — cat only exits once stdin reaches EOF
        var sut = new ProcessRunner();

        // Act
        var result = await sut.Run("/bin/cat", [], standardInput: "piped content");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("piped content");
    }

    [Fact]
    public async Task Run_ReadsOutputLargerThanThePipeBuffer()
    {
        // Arrange — 200 KB overruns the ~64 KB pipe buffer, deadlocking any implementation
        // that waits for exit before draining stdout. The timeout bounds that failure mode
        // so a regression fails the test instead of hanging the suite
        var sut = new ProcessRunner(timeout: TimeSpan.FromSeconds(10));

        // Act
        var result = await sut.Run("/bin/sh", ["-c", "yes hello | head -c 200000"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Length.Should().Be(200_000);
    }

    [Fact]
    public async Task Run_ReadsStandardErrorLargerThanThePipeBuffer()
    {
        // Arrange — the same overrun on the other pipe, which only stays clear
        // if both are drained concurrently rather than one after the other
        var sut = new ProcessRunner(timeout: TimeSpan.FromSeconds(10));

        // Act
        var result = await sut.Run("/bin/sh", ["-c", "yes hello | head -c 200000 >&2; exit 1"]);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Run_PassesEachArgumentSeparatelyWithoutShellSplitting()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act — a single argument containing spaces and quotes must arrive intact
        var result = await sut.Run("/bin/sh", ["-c", "printf '%s' \"$1\"", "sh", "a b \"c\"; rm -rf /"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("a b \"c\"; rm -rf /");
    }

    [Fact]
    public async Task Run_UsesTheExecutableGivenForEachCall()
    {
        // Arrange — one runner drives different executables, so nothing binds it to a single one
        var sut = new ProcessRunner();

        // Act
        var shell = await sut.Run("/bin/sh", ["-c", "printf from-sh"]);
        var cat = await sut.Run("/bin/cat", [], standardInput: "from-cat");

        // Assert
        shell.Value.Should().Be("from-sh");
        cat.Value.Should().Be("from-cat");
    }

    [Fact]
    public async Task Run_ReturnsFailOnNonZeroExitCode()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act
        var result = await sut.Run("/bin/sh", ["-c", "exit 3"]);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("3"));
    }

    [Fact]
    public async Task Run_IncludesStandardErrorInFailureMessage()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act
        var result = await sut.Run("/bin/sh", ["-c", "echo boom >&2; exit 1"]);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("boom"));
    }

    [Fact]
    public async Task Run_ReturnsFailWhenExecutableIsMissing()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act
        var result = await sut.Run("/nonexistent/executable", []);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("/nonexistent/executable"));
    }

    [Fact]
    public async Task Run_ReturnsFailWhenTimeoutElapses()
    {
        // Arrange
        var sut = new ProcessRunner(timeout: TimeSpan.FromMilliseconds(200));

        // Act
        var result = await sut.Run("/bin/sleep", ["30"]);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("timed out"));
    }

    [Fact]
    public async Task Run_ReturnsFailWhenCancelled()
    {
        // Arrange
        var sut = new ProcessRunner();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        var result = await sut.Run("/bin/sleep", ["30"], cancellationToken: cancellationTokenSource.Token);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("cancelled"));
    }
}
