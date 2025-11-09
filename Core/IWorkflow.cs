using FluentResults;

namespace Core;

public interface IWorkflow<TIn, TOut>
{
    Task<Result<TOut>> Execute(TIn input, CancellationToken cancellationToken = default);
}

public class Workflow<TIn, TOut> : IWorkflow<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<Result<TOut>>> _executor;

    private Workflow(Func<TIn, CancellationToken, Task<Result<TOut>>> executor) { _executor = executor; }

    public Task<Result<TOut>> Execute(TIn input, CancellationToken cancellationToken = default)
    {
        return _executor(input, cancellationToken);
    }

    public Task<Result<TOut>> Execute(CancellationToken cancellationToken = default) // Convenience overload for no-input workflows
    {
        return _executor((TIn)(object)Unit.Value, cancellationToken);
    }

    // Chain another tool to the workflow
    public Workflow<TIn, TNext> Add<TNext>(ITool<TOut, TNext> next) => new Workflow<TIn, TNext>(
    async (input, cancellationToken) =>
    {
        var intermediate = await _executor(input, cancellationToken).ConfigureAwait(false);
        if (intermediate.IsFailed)
        {
            return intermediate.ToResult<TNext>();
        }
        return await next.Transform(intermediate.Value, cancellationToken).ConfigureAwait(false);
    });

    // Factory for starting a new workflow from a tool
    public static Workflow<TIn, TOut> Create(ITool<TIn, TOut> tool) => new Workflow<TIn, TOut>(tool.Transform);
}

public static class Workflow
{
    public static Workflow<TIn, TOut> Add<TIn, TOut>(ITool<TIn, TOut> tool) => Workflow<TIn, TOut>.Create(tool);
    public static Workflow<Unit, TOut> Add<TOut>(ITool<Unit, TOut> tool) => Workflow<Unit, TOut>.Create(tool); // For tools that don't take input (i.e., ITool<Unit, TOut>)
}

public struct Unit
{
    public static readonly Unit Value = new Unit();
}
