using Core.Abstractions;
using FluentResults;

namespace Core.Tools.Workflow;

public class FirstOf<TIn, TOut> : ITool<TIn, TOut>
{
    private List<ITool<TIn, TOut>> _tools { get; init; }

    public FirstOf()
    {
        _tools = new();
    }

    public void Add(ITool<TIn, TOut> tool)
    {
        _tools.Add(tool);
    }

    public async Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default)
    {
        foreach (var tool in _tools)
        {
            try
            {
                return await tool.Transform(input, cancellationToken);
            }
            catch {}
        }

        throw new Exception("All tools failed");
    }
}
