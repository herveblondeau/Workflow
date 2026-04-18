using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;
using FluentResults;

namespace Core.Tools.Workflow;

/// <summary>
/// Runs multiple tools in parallel, then reduces their results into a single one using a reducer
/// Each tool:
/// - can take as input either the same type as the parallel tool, or no input
/// - can produce any output type; it's the reducer's job to merge them into the parallel tool's output type
/// </summary>
/// <example>
/// var parallelTool = ParallelTool
///     .Add(new Tool1()) // ITool<int, int>
///     .Add(new Tool2()) // ITool<int, int>
///     .Add(new Tool3()) // ITool<Unit, int>
///     .Reduce(results => // async version: .Reduce(async (results, ct) =>
///     {
///         return Result.Ok(results.OfType<Result<int>>().Where(r => r.IsSuccess).Sum(r => r.Value));
///     })
/// ;
/// </example>
public static class ParallelTool
{
    // Static Add entry point with input
    public static ParallelToolBuilder<TIn, object> Add<TIn, TOut>(ITool<TIn, TOut> first)
    {
        var list = new List<object> { first };
        return new ParallelToolBuilder<TIn, object>(list);
    }

    // Static Add entry point with no input (Unit)
    public static ParallelToolBuilder<Unit, object> Add<TOut>(ITool<Unit, TOut> first)
    {
        var list = new List<object> { first };
        return new ParallelToolBuilder<Unit, object>(list);
    }

    public sealed class ParallelToolBuilder<TIn, TOut> : ITool<TIn, TOut>
    {
        private readonly IReadOnlyList<object> _tools;
        private readonly Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>>? _asyncReducer;

        internal ParallelToolBuilder(IReadOnlyList<object> tools)
        {
            _tools = tools;
            _asyncReducer = null;
        }

        private ParallelToolBuilder(
            IReadOnlyList<object> tools,
            Func<IReadOnlyList<object>, CancellationToken, Task<Result<TOut>>>? asyncReducer)
        {
            _tools = tools;
            _asyncReducer = asyncReducer;
        }

        public ParallelToolBuilder<TIn, TOut> Add<TSub>(ITool<TIn, TSub> next)
        {
            var newTools = _tools.ToList();
            newTools.Add(next);
            return new ParallelToolBuilder<TIn, TOut>(newTools, _asyncReducer);
        }

        public ParallelToolBuilder<TIn, TOut> Add<TSub>(ITool<Unit, TSub> next)
        {
            var newTools = _tools.ToList();
            newTools.Add(next);
            return new ParallelToolBuilder<TIn, TOut>(newTools, _asyncReducer);
        }

        public ParallelToolBuilder<TIn, TNewOut> Reduce<TNewOut>(Func<IReadOnlyList<object>, CancellationToken, Task<Result<TNewOut>>> asyncReducer)
        {
            if (_asyncReducer != null)
                throw new InvalidOperationException("Reducer already defined.");

            return new ParallelToolBuilder<TIn, TNewOut>(_tools, asyncReducer);
        }

        public ParallelToolBuilder<TIn, TNewOut> Reduce<TNewOut>(Func<IReadOnlyList<object>, Result<TNewOut>> reducer)
        {
            Task<Result<TNewOut>> AsyncReducer(IReadOnlyList<object> results, CancellationToken ct) => Task.FromResult(reducer(results));
            return Reduce(AsyncReducer);
        }

        public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
        {
            if (_asyncReducer == null)
                throw new InvalidOperationException("Reducer function must be defined before executing.");

            var tasks = _tools.Select(tool => RunTool(tool, input, cancellationToken)).ToArray();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            var outResults = results.OfType<Result<TOut>>();
            var isSuccess = outResults
                .Any(r => r.IsSuccess)
            ;

            if (!isSuccess)
            {
                return Result.Fail(outResults.SelectMany(r => r.Errors));
            }

            return (await _asyncReducer(results, cancellationToken).ConfigureAwait(false))
                .WithSuccesses(outResults.SelectMany(r => r.Reasons).Select(r => new Success(r.Message)));
        }

        private static async Task<object> RunTool(object tool, TIn input, CancellationToken ct)
        {
            switch (tool)
            {
                case ITool<TIn, TOut> withInput:
                    return await withInput.Transform(input, ct).ConfigureAwait(false);

                case ITool<Unit, TOut> noInput:
                    return await noInput.Transform(Unit.Value, ct).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException("Invalid tool type in ParallelTool");
            }
        }
    }
}
