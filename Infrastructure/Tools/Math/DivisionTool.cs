using Core;
using Core.Models;
using FluentResults;

namespace Infrastructure.MathTools;

public class DivisionTool : ITool<BinaryMathInput, decimal>
{
    public Task<Result<decimal>> Transform(BinaryMathInput input, CancellationToken cancellationToken = default)
    {
        if (input.Right == 0)
        {
            return Task.FromResult(Result.Fail<decimal>($"{nameof(DivisionTool)}: cannot divide by zero"));
        }

        try
        {
            return Task.FromResult(Result.Ok(input.Left / input.Right));
        }
        catch (OverflowException ex)
        {
            return Task.FromResult(Result.Fail<decimal>(new Error($"{nameof(DivisionTool)}: overflow").CausedBy(ex)));
        }
        catch (DivideByZeroException ex)
        {
            return Task.FromResult(Result.Fail<decimal>(new Error($"{nameof(DivisionTool)}: cannot divide by zero").CausedBy(ex)));
        }
    }
}
