using System.Diagnostics;

namespace Infrastructure.Processes;

/// <summary>
/// Default <see cref="IProcessRunner"/> backed by <see cref="Process"/>. Reads stdout and
/// stderr concurrently before awaiting exit to avoid pipe-buffer deadlocks on large output.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
