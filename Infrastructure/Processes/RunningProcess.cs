using System.Diagnostics;
using System.Text;
using FluentResults;

namespace Infrastructure.Processes;

/// <summary>
/// Handle over a started child process, returned by <see cref="ProcessRunner.Start"/>
/// </summary>
internal sealed class RunningProcess : IRunningProcess
{
    private const int _retainedErrorLength = 8 * 1024;

    private readonly Process _process;
    private readonly string _executablePath;
    private readonly Task<string> _drainStandardError;

    private Result? _stopResult;

    internal RunningProcess(Process process, string executablePath)
    {
        _process = process;
        _executablePath = executablePath;

        // Standard output is the caller's to drain, but nobody else would read standard error.
        // A long-running command narrates its progress there — ffmpeg does it per frame — and
        // the pipe holds only ~64 KB, so leaving it unread blocks the child mid-capture.
        // Only the tail is kept: that is the part explaining an unexpected exit
        _drainStandardError = _readTail(process.StandardError);

        // A process reading stdin keeps waiting until it sees EOF, and nothing streams input here
        process.StandardInput.Close();
    }

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public async Task<Result> Stop(CancellationToken cancellationToken = default)
    {
        // DisposeAsync stops as well, so a caller that already stopped must not see
        // its clean shutdown turn into an error
        if (_stopResult is not null)
        {
            return _stopResult;
        }

        try
        {
            // Killing is how a streamed command ends, so the non-zero exit code that follows
            // says nothing. A process that had already exited is the one worth reporting on
            var exitedOnItsOwn = _process.HasExited;
            if (!exitedOnItsOwn)
            {
                _kill();
            }

            await _process.WaitForExitAsync(cancellationToken);
            var error = await _drainStandardError;

            _stopResult = exitedOnItsOwn && _process.ExitCode != 0
                ? _exitedWithError(error)
                : Result.Ok();
        }
        catch (Exception ex)
        {
            _kill();
            _stopResult = Result.Fail(new Error($"{nameof(ProcessRunner)}: {_executablePath} failed to stop").CausedBy(ex));
        }

        return _stopResult;
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
        _process.Dispose();
    }

    private Result _exitedWithError(string error)
    {
        var message = $"{nameof(ProcessRunner)}: {_executablePath} exited with code {_process.ExitCode}";
        return Result.Fail(string.IsNullOrWhiteSpace(error) ? message : $"{message} ({error.Trim()})");
    }

    private static async Task<string> _readTail(StreamReader reader)
    {
        var buffer = new char[4096];
        var tail = new StringBuilder();

        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            tail.Append(buffer, 0, read);
            if (tail.Length > _retainedErrorLength)
            {
                tail.Remove(0, tail.Length - _retainedErrorLength);
            }
        }

        return tail.ToString();
    }

    private void _kill()
    {
        try
        {
            // Children outlive the process they were spawned from unless the whole tree goes
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited on its own in the meantime
        }
    }
}
