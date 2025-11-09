using Core;
using FluentResults;

namespace Infrastructure.Workflow;


/// <summary>
/// Runs multiple tools in sequence
/// Returns the result of the first tool that succeeds
/// Fails if all tools fail
/// </summary>
/// <example>
/// var firstSuccessfulTool = new FirstSuccessfulTool<int, int>();
/// firstSuccessfulTool
///     .Add(new Tool1())
///     .Add(new Tool2())
///     .Add(new Tool3())
/// </example>
public class FirstSuccessfulTool<TIn, TOut> : ITool<TIn, TOut>
{
    private List<ITool<TIn, TOut>> _tools { get; init; }

    public FirstSuccessfulTool()
    {
        _tools = new();
    }

    public FirstSuccessfulTool<TIn, TOut> Add(ITool<TIn, TOut> tool)
    {
        _tools.Add(tool);
        return this;
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        var aggregatedErrors = new List<IError>();

        foreach (var tool in _tools)
        {
            var result = await tool.Transform(input, cancellationToken);
            if (result.IsSuccess)
            {
                // TODO: add the previously aggregated errors if possible
                return result;
            }

            aggregatedErrors.AddRange(result.Errors);
        }

        return Result.Fail<TOut>(aggregatedErrors);
    }
}
