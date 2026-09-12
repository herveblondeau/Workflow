namespace Infrastructure.Processes;

/// <summary>
/// Result of running an external process to completion.
/// </summary>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Seam over <see cref="System.Diagnostics.Process"/> so tools that shell out to
/// external binaries can be unit-tested without spawning a real process.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/> to completion and
    /// returns its exit code and captured output. Arguments are passed as a list so each one
    /// maps to a single argv entry, avoiding shell-quoting bugs.
    /// </summary>
    Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
