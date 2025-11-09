using Core;
using FluentResults;

namespace Core.Tools.Workflow;

/// <summary>
/// Runs one of two tools depending on a condition
/// </summary>
/// <example>
/// var conditionalTool = new ConditionalTool<int, string>(
///     input => input == 42,
///     new Tool1(),
///     new Tool2()
/// );
/// </example>
public class ConditionalTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly Func<TIn, bool> _predicate;
    private readonly ITool<TIn, TOut> _trueTool;
    private readonly ITool<TIn, TOut> _falseTool;

    public ConditionalTool(
        Func<TIn, bool> predicate,
        ITool<TIn, TOut> trueTool,
        ITool<TIn, TOut> falseTool)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _trueTool = trueTool ?? throw new ArgumentNullException(nameof(trueTool));
        _falseTool = falseTool ?? throw new ArgumentNullException(nameof(falseTool));
    }

    public Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        if (_predicate(input))
        {
            return _trueTool.Transform(input, cancellationToken);
        }
        else
        {
            return _falseTool.Transform(input, cancellationToken);
        }
    }
}
