using FluentResults;

namespace Infrastructure.Processes;

/// <summary>
/// A child process still running, whose lifetime the caller drives
/// </summary>
public interface IRunningProcess : IAsyncDisposable
{
    /// <summary>
    /// The process's standard output, readable while it runs
    /// The caller must keep reading it: an unread pipe fills at around 64 KB and blocks the child
    /// </summary>
    Stream StandardOutput { get; }

    /// <summary>
    /// Ends the process and reports how it went. Succeeds when the process was still
    /// running — being killed is the normal way a streamed command ends — and fails
    /// only for one that had already given up on its own
    /// </summary>
    Task<Result> Stop(CancellationToken cancellationToken = default);
}
