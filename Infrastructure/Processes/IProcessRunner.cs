using FluentResults;

namespace Infrastructure.Processes;

/// <summary>
/// Runs an external command-line executable and captures its output
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the executable with the given arguments and returns its standard output
    /// </summary>
    /// <param name="executablePath">
    /// An absolute path or a name resolvable through PATH. Named per call rather than
    /// per instance so a tool always picks its own executable, and an injected runner
    /// cannot silently point it at a different one
    /// </param>
    /// <param name="arguments">
    /// One entry per argument — never a pre-joined command line, so that arguments
    /// containing spaces or quotes are passed through verbatim
    /// </param>
    /// <param name="standardInput">
    /// Piped to the process's standard input when supplied, for commands that read
    /// their content from there rather than from an argument
    /// </param>
    Task<Result<string>> Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a long-running process and hands back a handle to it, for output that has to be
    /// consumed while the process runs rather than collected once it exits
    /// The caller owns the lifetime, so no timeout applies — it ends the process through the handle
    /// </summary>
    /// <param name="executablePath">Named per call, for the same reason as in <see cref="Run"/></param>
    /// <param name="arguments">One entry per argument, for the same reason as in <see cref="Run"/></param>
    Result<IRunningProcess> Start(string executablePath, IReadOnlyList<string> arguments);
}
