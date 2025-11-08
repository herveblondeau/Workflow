using Core.Abstractions;
using FluentResults;

namespace Core.Infrastructure.Tools.Workflow;

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
            return Result.Fail<TOut>(errors);

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
