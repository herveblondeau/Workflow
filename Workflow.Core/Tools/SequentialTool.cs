using Core.Abstractions;
using FluentResults;

namespace Core.Infrastructure.Tools.Workflow;

public class SequentialTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<Result<TOut>>> _executor;

    private SequentialTool(Func<TIn, CancellationToken, Task<Result<TOut>>> executor)
    {
        _executor = executor;
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
        => await _executor(input, cancellationToken).ConfigureAwait(false);

    // Factory for starting a sequence from a single tool
    public static SequentialTool<TIn, TOut> From(ITool<TIn, TOut> tool)
        => new SequentialTool<TIn, TOut>(tool.Transform);

    // Adds a new tool with matching input/output transition
    public SequentialTool<TIn, TNext> Add<TNext>(ITool<TOut, TNext> next)
        => new SequentialTool<TIn, TNext>(
            async (input, cancellationToken) =>
            {
                var intermediate = await _executor(input, cancellationToken).ConfigureAwait(false);

                // stop chain on failure
                if (intermediate.IsFailed)
                {
                    return intermediate.ToResult<TNext>();
                }

                return await next.Transform(intermediate.Value, cancellationToken).ConfigureAwait(false);
            });
}
