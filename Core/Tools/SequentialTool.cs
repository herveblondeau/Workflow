using Core;
using FluentResults;

namespace Core.Tools.Workflow;

/// <summary>
/// Runs multiple tools in sequence
/// All tools must succeed for the sequence to succeed
/// </summary>
/// <example>
/// var sequentialTool = SequentialTool
///     .Add(new Tool1())
///     .Add(new Tool2())
///     .Add(new Tool3());
/// </example>
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
    public static SequentialTool<TIn, TOut> Create(ITool<TIn, TOut> tool) => new SequentialTool<TIn, TOut>(tool.Transform);

    // Adds a new tool with matching input/output transition
    public SequentialTool<TIn, TNext> Add<TNext>(ITool<TOut, TNext> next)
        => new SequentialTool<TIn, TNext>(
            async (input, cancellationToken) =>
            {
                var intermediate = await _executor(input, cancellationToken).ConfigureAwait(false);

                // Stop chain on failure
                if (intermediate.IsFailed)
                {
                    return intermediate.ToResult<TNext>();
                }

                return await next.Transform(intermediate.Value, cancellationToken).ConfigureAwait(false);
            });
}

public static class SequentialTool
{
    public static SequentialTool<TIn, TOut> Add<TIn, TOut>(ITool<TIn, TOut> tool) => SequentialTool<TIn, TOut>.Create(tool);
    public static SequentialTool<Unit, TOut> Add<TOut>(ITool<Unit, TOut> tool) => SequentialTool<Unit, TOut>.Create(tool); // For tools that don't take input (i.e., ITool<Unit, TOut>)
}
