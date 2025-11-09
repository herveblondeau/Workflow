using Core;
using FluentResults;

namespace Core.Tools.Workflow;

/// <summary>
/// Runs one of two tools depending on a condition
/// </summary>
/// <example>
/// var conditionalTool = ConditionalTool.If(
///     condition: n => n >= 30,
///     thenTool: new Tool1(),
///     elseTool: new Tool2()
/// );
/// </example>
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;

public class ConditionalTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly Func<TIn, bool> _condition;
    private readonly ITool<TIn, TOut> _thenTool;
    private readonly ITool<TIn, TOut> _elseTool;

    public ConditionalTool(
        Func<TIn, bool> condition,
        ITool<TIn, TOut> thenTool,
        ITool<TIn, TOut> elseTool)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenTool = thenTool ?? throw new ArgumentNullException(nameof(thenTool));
        _elseTool = elseTool ?? throw new ArgumentNullException(nameof(elseTool));
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        var conditionFulfilled = _condition(input);
        var selectedTool = conditionFulfilled ? _thenTool : _elseTool;
        return (await selectedTool.Transform(input, cancellationToken).ConfigureAwait(false)).WithSuccess(conditionFulfilled ? $"{nameof(ConditionalTool)}: condition fulfilled" : $"{nameof(ConditionalTool)}: condition not fulfilled");
    }
}

// Helper façade for type inference
public static class ConditionalTool
{
    public static ConditionalTool<TIn, TOut> If<TIn, TOut>(
        Func<TIn, bool> condition,
        ITool<TIn, TOut> thenTool,
        ITool<TIn, TOut> elseTool)
        => new ConditionalTool<TIn, TOut>(condition, thenTool, elseTool);

    // public static ConditionalTool<TIn, TOut> If<TIn, TOut>(
    //     Func<TIn, bool> condition,
    //     ITool<TIn, TOut> thenTool)
    //     => new ConditionalTool<TIn, TOut>(condition, thenTool, new NoOpTool<TIn, TOut>());
}
