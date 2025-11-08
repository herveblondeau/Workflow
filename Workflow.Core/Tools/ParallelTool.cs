using Core.Abstractions;
using FluentResults;

namespace Core.Infrastructure.Tools.Workflow;

/// <summary>
/// Runs multiple tools in parallel, then reduces their results into a single one using a reducer
/// Each tool:
/// - can take as input either the same type as the parallel tool, or no input
/// - can produce any output type; it's the reducer's job to merge them into the parallel tool's output type
/// </summary>
/// <example>
/// var parallelTool = new ParallelTool<int, string>(
///     [
///         new ParallelTool<int, int>.ValueSubTool<int, int>(new Tool1()), // tool that needs an input
///         new ParallelTool<int, int>.ValueSubTool<int, string>(new Tool2()), // tool that needs an input
///         new ParallelTool<int, int>.ValueSubTool<int, double>(new Tool3()), // tool that needs an input
///         new ParallelTool<int, string>.UnitSubTool<int, string>(new Tool4()) // tool with no input
///     ],
///     outputs =>
///     {
///         var myInt = (int)outputs[0];
///         var myString = (string)outputs[1];
///         var myDouble = (double)outputs[2];
///         var myString2 = (string)outputs[3];
///         return "A string computed from all output results";
///     }
/// );
/// </example>
public interface IParallelSubtool<TIn>
{
    Task<Result<object>> Execute(TIn input, CancellationToken cancellationToken);
}

public class ParallelTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly IReadOnlyList<IParallelSubtool<TIn>> _subtools;
    private readonly Func<IReadOnlyList<object>, TOut> _reducer;

    public ParallelTool(
        IEnumerable<IParallelSubtool<TIn>> subtools,
        Func<IReadOnlyList<object>, TOut> reducer)
    {
        _subtools = subtools.ToList();
        _reducer = reducer;
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        var tasks = _subtools.Select(t => t.Execute(input, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var errors = results.Where(r => r.IsFailed).SelectMany(r => r.Errors).ToList();
        if (errors.Any())
        {
            return Result.Fail<TOut>(errors);
        }

        var values = results.Select(r => r.Value!).ToList();
        var combined = _reducer(values);
        return Result.Ok(combined);
    }

    public class ValueSubTool<TIn, TOut> : IParallelSubtool<TIn>
    {
        private readonly ITool<TIn, TOut> _tool;

        public ValueSubTool(ITool<TIn, TOut> tool)
        {
            _tool = tool;
        }

        public async Task<Result<object>> Execute(TIn input, CancellationToken cancellationToken)
        {
            var result = await _tool.Transform(input, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? Result.Ok((object)result.Value!)
                : Result.Fail<object>(result.Errors);
        }
    }

    public class UnitSubTool<TIn, TOut> : IParallelSubtool<TIn>
    {
        private readonly ITool<Unit, TOut> _tool;

        public UnitSubTool(ITool<Unit, TOut> tool)
        {
            _tool = tool;
        }

        public async Task<Result<object>> Execute(TIn input, CancellationToken cancellationToken)
        {
            var result = await _tool.Transform(Unit.Value, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? Result.Ok((object)result.Value!)
                : Result.Fail<object>(result.Errors);
        }
    }
}
