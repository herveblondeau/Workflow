using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Core;

namespace Infrastructure.Workflow;

/// <summary>
/// Runs multiple tools in sequence
/// Returns the result of the first tool that succeeds
/// Fails if all tools fail
/// </summary>
/// <example>
/// var firstSuccessfulTool = FirstSuccessfulTool
///     .Add(new Tool1())
///     .Add(new Tool2())
///     .Add(new Tool3());
/// </example>

public class FirstSuccessfulTool<TIn, TOut> : ITool<TIn, TOut>
{
    private readonly List<ITool<TIn, TOut>> _tools;

    private FirstSuccessfulTool(ITool<TIn, TOut> tool)
    {
        _tools = new List<ITool<TIn, TOut>>()
        {
            tool
        };
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        List<IReason> reasons = new();

        foreach (var tool in _tools)
        {
            var result = await tool.Transform(input, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return result.WithSuccesses(reasons.Select(r => new Success(r.Message)));
            }

            reasons.AddRange(result.Reasons);
        }

        return Result.Fail<TOut>($"All {_tools.Count} tools failed.")
            .WithReasons(reasons);
    }

    // Factory for starting a sequence from a single tool
    public static FirstSuccessfulTool<TIn, TOut> Create(ITool<TIn, TOut> tool) => new FirstSuccessfulTool<TIn, TOut>(tool);

    // Adds a new tool with matching input/output transition
    public FirstSuccessfulTool<TIn, TOut> Add(ITool<TIn, TOut> next)
    {
        _tools.Add(next);
        return this;
    }
}

public static class FirstSuccessfulTool
{
    public static FirstSuccessfulTool<TIn, TOut> Add<TIn, TOut>(ITool<TIn, TOut> tool) => FirstSuccessfulTool<TIn, TOut>.Create(tool);
    public static FirstSuccessfulTool<Unit, TOut> Add<TOut>(ITool<Unit, TOut> tool) => FirstSuccessfulTool<Unit, TOut>.Create(tool); // For tools that don't take input (i.e., ITool<Unit, TOut>)
}
