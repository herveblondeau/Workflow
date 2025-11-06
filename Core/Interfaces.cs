namespace Core;

public interface ITool<TIn, TOut>
{
    // ToolState State { get; }
    Task<TOut> Transform(TIn input, CancellationToken cancellationToken = default);
}

public struct Unit
{
    public static readonly Unit Value = new Unit();
}

public enum ToolState
{
    Idle,
    Starting,
    Running,
    Stopping,
}

public interface IPipeline<TIn, TOut>
{
    Task<TOut> Execute(TIn input, CancellationToken cancellationToken = default);
}

public class Pipeline<TIn, TOut> : IPipeline<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<TOut>> _executor;

    private Pipeline(Func<TIn, CancellationToken, Task<TOut>> executor) { _executor = executor; }

    public Task<TOut> Execute(TIn input, CancellationToken cancellationToken = default)
    {
        return _executor(input, cancellationToken);
    }

    public Task<TOut> Execute(CancellationToken cancellationToken = default) // Convenience overload for no-input pipelines
    {
        return _executor((TIn)(object)Unit.Value, cancellationToken);
    }

    // Chain another tool to the pipeline
    public Pipeline<TIn, TNext> Add<TNext>(ITool<TOut, TNext> next) => new Pipeline<TIn, TNext>(
    async (input, cancellationToken) =>
    {
        var intermediate = await _executor(input, cancellationToken).ConfigureAwait(false);
        return await next.Transform(intermediate, cancellationToken).ConfigureAwait(false);
    });

    // Factory for starting a new pipeline from a tool
    public static Pipeline<TIn, TOut> Create(ITool<TIn, TOut> tool) => new Pipeline<TIn, TOut>(tool.Transform);
}

public static class Pipeline
{
    public static Pipeline<TIn, TOut> Add<TIn, TOut>(ITool<TIn, TOut> tool) => Pipeline<TIn, TOut>.Create(tool);
    public static Pipeline<Unit, TOut> Add<TOut>(ITool<Unit, TOut> tool) => Pipeline<Unit, TOut>.Create(tool); // For tools that don't take input (i.e., ITool<Unit, TOut>)
}
