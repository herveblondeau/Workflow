namespace Main;

public interface ITool<TIn, TOut>
{
    // ToolState State { get; }
    Task<TOut> Transform(TIn input, CancellationToken cancellationToken = default);
}

// public enum ToolState
// {
//     Idle,
//     Starting,
//     Running,
//     Stopping,
// }

public class ReverserTool : ITool<string, string>
{
    public Task<string> Transform(string input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Join("", input.Reverse()));
    }
}

public class CounterTool : ITool<string, int>
{
    public Task<int> Transform(string input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(input.Length);
    }
}

public class MultiplierTool : ITool<int, int>
{
    public int Multiplier { get; private set; }

    public MultiplierTool(int multiplier)
    {
        Multiplier = multiplier;
    }

    public Task<int> Transform(int input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(input * Multiplier);
    }
}

public interface IPipeline<TIn, TOut>
{
    Task<TOut> Execute(TIn input, CancellationToken cancellationToken = default);
}

public class Pipeline<TIn, TOut> : IPipeline<TIn, TOut>, ITool<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<TOut>> _executor;

    private Pipeline(Func<TIn, CancellationToken, Task<TOut>> executor) { _executor = executor; }

    public Task<TOut> Execute(TIn input, CancellationToken cancellationToken = default) => _executor(input, cancellationToken);
    public Task<TOut> Transform(TIn input, CancellationToken cancellationToken = default) => _executor(input, cancellationToken);

    // Add another tool to the pipeline
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
}
