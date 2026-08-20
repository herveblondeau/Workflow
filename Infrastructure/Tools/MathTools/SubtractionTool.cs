using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.MathTools;

public class SubtractionTool : ITool<BinaryMathInput, decimal>
{
    public Task<Result<decimal>> Transform(BinaryMathInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(Result.Ok(input.Left - input.Right));
        }
        catch (OverflowException ex)
        {
            return Task.FromResult(Result.Fail<decimal>(new Error($"{nameof(SubtractionTool)}: overflow").CausedBy(ex)));
        }
    }
}
