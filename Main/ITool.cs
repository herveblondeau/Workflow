namespace Main;

public interface ITool<TIn, TOut>
{
    Task<TOut> ProcessAsync(TIn input, CancellationToken cancellationToken = default);
}

public abstract class ToolBase<TIn, TOut> : ITool<TIn, TOut>
{
    public ToolState State { get; protected set; } = ToolState.Idle;

    public abstract Task<TOut> ProcessAsync(TIn input, CancellationToken cancellationToken = default);
}

public enum ToolState
{
    Idle,
    Starting,
    Running,
    Stopping,
}
