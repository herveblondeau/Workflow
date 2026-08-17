using System.Text;
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

    [Fact]
    public async Task Start_ReadsOutputWhileTheProcessIsStillRunning()
    {
        // Arrange — the point of Start over Run: output arrives before the process exits
        var sut = new ProcessRunner();

        // Act
        var started = sut.Start("/bin/sh", ["-c", "printf first; sleep 30"]);

        // Assert
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;
        var output = await ReadExactly(running.StandardOutput, "first".Length);
        output.Should().Be("first");
    }

    [Fact]
    public async Task Start_DrainsStandardErrorSoTheProcessIsNotBlockedByIt()
    {
        // Arrange — a long-running command narrates its progress on stderr (ffmpeg does it
        // per frame), so an undrained pipe fills at ~64 KB and the child blocks mid-write.
        // Here the write to stdout only happens once the 200 KB of stderr has gone through
        var sut = new ProcessRunner();

        // Act
        var started = sut.Start("/bin/sh", ["-c", "yes noise | head -c 200000 >&2; printf done; sleep 30"]);

        // Assert
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;
        var output = await ReadExactly(running.StandardOutput, "done".Length);
        output.Should().Be("done");
    }

    [Fact]
    public async Task Start_PassesEachArgumentSeparatelyWithoutShellSplitting()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act — a single argument containing spaces and quotes must arrive intact
        var started = sut.Start("/bin/sh", ["-c", "printf '%s' \"$1\"", "sh", "a b \"c\"; rm -rf /"]);

        // Assert
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;
        using var reader = new StreamReader(running.StandardOutput);
        var output = await WithTimeout(reader.ReadToEndAsync(), "the process to close stdout");
        output.Should().Be("a b \"c\"; rm -rf /");
    }

    [Fact]
    public void Start_ReturnsFailWhenExecutableIsMissing()
    {
        // Arrange
        var sut = new ProcessRunner();

        // Act
        var started = sut.Start("/nonexistent/executable", []);

        // Assert
        started.IsFailed.Should().BeTrue();
        started.Errors.Should().Contain(e => e.Message.Contains("/nonexistent/executable"));
    }

    [Fact]
    public async Task Stop_KillsTheEntireProcessTree()
    {
        // Arrange — the shell backgrounds a child that outlives it. Killing the shell alone
        // leaves that child running, so the marker file it writes is the evidence
        var sut = new ProcessRunner();
        var marker = Path.Combine(Path.GetTempPath(), $"process-runner-tree-kill-{Guid.NewGuid():N}");
        var started = sut.Start("/bin/sh", ["-c", $"(sleep 2; printf alive > '{marker}') & printf ready; wait"]);
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;
        await ReadExactly(running.StandardOutput, "ready".Length);

        // Act
        var stopped = await running.Stop();

        // Assert
        stopped.IsSuccess.Should().BeTrue();
        await Task.Delay(TimeSpan.FromSeconds(4));
        File.Exists(marker).Should().BeFalse();
    }

    // Killing is how a streamed capture ends, so the non-zero exit code it produces
    // must not be reported as a failure
    [Fact]
    public async Task Stop_SucceedsWhenItKillsARunningProcess()
    {
        // Arrange
        var sut = new ProcessRunner();
        var started = sut.Start("/bin/sleep", ["30"]);
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;

        // Act
        var stopped = await running.Stop();

        // Assert
        stopped.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Stop_ReturnsFailWhenTheProcessAlreadyDiedOnItsOwn()
    {
        // Arrange — a process that gave up before being stopped has something to report
        var sut = new ProcessRunner();
        var started = sut.Start("/bin/sh", ["-c", "echo boom >&2; exit 3"]);
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;
        using var reader = new StreamReader(running.StandardOutput);
        await WithTimeout(reader.ReadToEndAsync(), "the process to exit");

        // Act
        var stopped = await running.Stop();

        // Assert
        stopped.IsFailed.Should().BeTrue();
        stopped.Errors.Should().Contain(e => e.Message.Contains("3"));
        stopped.Errors.Should().Contain(e => e.Message.Contains("boom"));
    }

    [Fact]
    public async Task Stop_IsIdempotent()
    {
        // Arrange — DisposeAsync stops too, so a caller that already stopped must not
        // see that turn into an error
        var sut = new ProcessRunner();
        var started = sut.Start("/bin/sleep", ["30"]);
        started.IsSuccess.Should().BeTrue();
        await using var running = started.Value;

        // Act
        var first = await running.Stop();
        var second = await running.Stop();

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_StopsAProcessThatWasNeverStopped()
    {
        // Arrange
        var sut = new ProcessRunner();
        var marker = Path.Combine(Path.GetTempPath(), $"process-runner-dispose-{Guid.NewGuid():N}");
        var started = sut.Start("/bin/sh", ["-c", $"printf ready; sleep 2; printf alive > '{marker}'"]);
        started.IsSuccess.Should().BeTrue();

        // Act
        await using (var running = started.Value)
        {
            await ReadExactly(running.StandardOutput, "ready".Length);
        }

        // Assert
        await Task.Delay(TimeSpan.FromSeconds(4));
        File.Exists(marker).Should().BeFalse();
    }

    private static async Task<string> ReadExactly(Stream stream, int byteCount)
    {
        var buffer = new byte[byteCount];
        var read = 0;

        while (read < byteCount)
        {
            var readTask = stream.ReadAsync(buffer.AsMemory(read, byteCount - read)).AsTask();
            var bytes = await WithTimeout(readTask, $"{byteCount - read} more byte(s) of output");
            if (bytes == 0)
            {
                break;
            }

            read += bytes;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    // Bounds every read so a regression that blocks the child fails the test
    // instead of hanging the suite
    private static async Task<T> WithTimeout<T>(Task<T> task, string waitingFor)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed != task)
        {
            throw new TimeoutException($"timed out waiting for {waitingFor}");
        }

        return await task;
    }
}
