namespace Core.Abstractions;

public interface ITool<TIn, TOut>
{
    // ToolState State { get; }
    Task<TOut> Transform(TIn input, CancellationToken cancellationToken = default);
}

//public enum ToolState
//{
//    Idle,
//    Starting,
//    Running,
//    Stopping,
//}
