using System.Diagnostics;
using FluentResults;

namespace Infrastructure.Processes;

/// <summary>
/// Runs an external executable as a child process
/// The executable must be an absolute path or resolvable through PATH
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _timeout;

    public ProcessRunner(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? _defaultTimeout;
    }

    public async Task<Result<string>> Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList escapes each entry, so arguments are never re-parsed as shell syntax
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process == null)
            {
                return Result.Fail($"{nameof(ProcessRunner)}: {executablePath} process failed to start");
            }

            // Both pipes must be drained while the process runs. A redirected pipe holds only
            // ~64 KB; once it fills, the child blocks on write and never reaches exit, while a
            // parent that waits for exit before reading never frees any space — a deadlock with
            // no natural end. Starting both reads first keeps the pipes moving throughout.
            var readOutput = process.StandardOutput.ReadToEndAsync();
            var readError = process.StandardError.ReadToEndAsync();

            await _writeStandardInput(process, standardInput, timeoutSource.Token);

            await process.WaitForExitAsync(timeoutSource.Token);

            var output = await readOutput;
            var error = await readError;

            if (process.ExitCode != 0)
            {
                var message = $"{nameof(ProcessRunner)}: {executablePath} exited with code {process.ExitCode}";
                return Result.Fail(string.IsNullOrWhiteSpace(error) ? message : $"{message} ({error.Trim()})");
            }

            return Result.Ok(output);
        }
        catch (OperationCanceledException ex)
        {
            _kill(process);

            // Only the linked source fired when the caller's own token is still unset
            var message = cancellationToken.IsCancellationRequested
                ? $"{nameof(ProcessRunner)}: {executablePath} was cancelled"
                : $"{nameof(ProcessRunner)}: {executablePath} timed out after {_timeout.TotalSeconds:0.##}s";

            return Result.Fail(new Error(message).CausedBy(ex));
        }
        catch (Exception ex)
        {
            _kill(process);
            return Result.Fail(new Error($"{nameof(ProcessRunner)}: {executablePath} failed to run").CausedBy(ex));
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task _writeStandardInput(Process process, string? standardInput, CancellationToken cancellationToken)
    {
        if (standardInput != null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
        }

        // Closed unconditionally: a process reading stdin keeps waiting until it sees EOF
        process.StandardInput.Close();
    }

    private static void _kill(Process? process)
    {
        try
        {
            // Children outlive the process they were spawned from unless the whole tree goes
            process?.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited on its own in the meantime
        }
    }
}
