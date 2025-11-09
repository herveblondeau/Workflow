using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;
using FluentResults;

namespace Infrastructure.Tools.Workflow;

/// <summary>
/// Runs multiple tools in parallel, then reduces their results into a single one using a reducer
/// Each tool:
/// - can take as input either the same type as the parallel tool, or no input
/// - can produce any output type; it's the reducer's job to merge them into the parallel tool's output type
/// </summary>
/// <example>
/// var parallelTool = ParallelTool.Create(
///     new Tool1(), // ITool<int, int>
///     async (results, CancellationToken) => // reducer
///     {
///         var successes = results
///             .OfType<Result<int>>()
///             .Where(r => r.IsSuccess)
///             .Select(r => r.Value);
///
///         return Result.Ok(successes.Sum());
///     })
///     .Add(new Tool2()) // ITool<int, int>
///     .Add(new ToolD()) // ITool<Unit, int>
/// ;
/// </example>
public class ParallelTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly IReadOnlyList<object> _tools;
    private readonly Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> _asyncReducer;

    public ParallelTool(IEnumerable<object> tools, Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> asyncReducer)
    {
        _tools = tools.ToList();
        _asyncReducer = asyncReducer ?? throw new ArgumentNullException(nameof(asyncReducer));
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        var tasks = _tools.Select(tool => RunTool(tool, input, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return await _asyncReducer(results, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> RunTool(object tool, TIn input, CancellationToken ct)
    {
        switch (tool)
        {
            case ITool<TIn, TOut> withInput:
                {
                    var result = await withInput.Transform(input, ct).ConfigureAwait(false);
                    return result;
                }
            case ITool<Unit, TOut> noInput:
                {
                    var result = await noInput.Transform(Unit.Value, ct).ConfigureAwait(false);
                    return result;
                }
            default:
                throw new InvalidOperationException("Invalid tool type in ParallelTool");
        }
    }

    // Fluent composition

    public ParallelTool<TIn, TOut> Add<TSub>(ITool<TIn, TSub> tool)
        => new ParallelTool<TIn, TOut>(_tools.Concat(new[] { tool }), _asyncReducer);

    public ParallelTool<TIn, TOut> Add<TSub>(ITool<Unit, TSub> tool)
        => new ParallelTool<TIn, TOut>(_tools.Concat(new[] { tool }), _asyncReducer);

    public static ParallelTool<TIn, TOut> Create<TSub>(
        ITool<TIn, TSub> first,
        Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> asyncReducer)
        => new ParallelTool<TIn, TOut>(new object[] { first }, asyncReducer);

    public static ParallelTool<TIn, TOut> Create<TSub>(
        ITool<Unit, TSub> first,
        Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> asyncReducer)
        => new ParallelTool<TIn, TOut>(new object[] { first }, asyncReducer);
}

// Non-generic façade for inference

public static class ParallelTool
{
    public static ParallelTool<TIn, TOut> Create<TIn, TOut, TSub>(
        ITool<TIn, TSub> tool,
        Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> asyncReducer)
        => ParallelTool<TIn, TOut>.Create(tool, asyncReducer);

    public static ParallelTool<TIn, TOut> Create<TIn, TOut, TSub>(
        ITool<Unit, TSub> tool,
        Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>> asyncReducer)
        => ParallelTool<TIn, TOut>.Create(tool, asyncReducer);
}
