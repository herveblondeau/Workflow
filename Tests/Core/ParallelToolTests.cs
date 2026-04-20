using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class ParallelToolTests
{
    // Each tool returns its 1-based index on success (tool1 → 1, tool2 → 2, ...).
    // Tool at failAtIndex returns a failure; all others succeed.
    // failAtIndex = 0 means all tools succeed.
    // Expected sum when all succeed: 1+2+...+N = N*(N+1)/2.
    // Expected sum when tool k fails: N*(N+1)/2 - k.
    private static ITool<int, int>[] BuildTools(int totalTools, int failAtIndex = 0)
    {
        var tools = Enumerable.Range(1, totalTools)
            .Select(_ => Substitute.For<ITool<int, int>>())
            .ToArray();

        for (int i = 0; i < totalTools; i++)
        {
            int oneBased = i + 1;
            if (failAtIndex > 0 && oneBased == failAtIndex)
                tools[i].Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(Result.Fail<int>($"tool{oneBased} error")));
            else
                tools[i].Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(Result.Ok(oneBased)));
        }

        return tools;
    }

    private static ITool<int, int> BuildSut(ITool<int, int>[] tools)
        => tools.Skip(1)
            .Aggregate(ParallelTool.Add(tools[0]), (acc, t) => acc.Add(t))
            .Reduce<int>(results => Result.Ok(results.OfType<Result<int>>().Where(r => r.IsSuccess).Sum(r => r.Value)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Transform_RunsAllToolsAndPassesResultsToReducer(int totalTools)
    {
        // Arrange
        var tools = BuildTools(totalTools);
        IReadOnlyList<object>? captured = null;
        var sut = tools.Skip(1)
            .Aggregate(ParallelTool.Add(tools[0]), (acc, t) => acc.Add(t))
            .Reduce<int>(results =>
            {
                captured = results;
                return Result.Ok(results.OfType<Result<int>>().Where(r => r.IsSuccess).Sum(r => r.Value));
            });

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().HaveCount(totalTools);
        foreach (var tool in tools)
            await tool.Received(1).Transform(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)] // result = 1
    [InlineData(2)] // result = 3
    [InlineData(3)] // result = 6
    [InlineData(5)] // result = 15
    public async Task Transform_ReducerReceivesResultsFromAllTools(int totalTools)
    {
        // Arrange
        var sut = BuildSut(BuildTools(totalTools));

        // Act
        var result = await sut.Transform(0);

        // Assert: sum of 1..N proves all tools contributed
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(totalTools * (totalTools + 1) / 2);
    }

    [Theory]
    [InlineData(2, 1)] // tool1 fails, tool2 succeeds → sum = 3 - 1 = 2
    [InlineData(2, 2)] // tool1 succeeds, tool2 fails → sum = 3 - 2 = 1
    [InlineData(3, 2)] // tool2 fails → sum = 6 - 2 = 4
    [InlineData(5, 3)] // tool3 fails → sum = 15 - 3 = 12
    public async Task Transform_CallsReducerWithAllResultsWhenOneToolFails(int totalTools, int failAtIndex)
    {
        // Arrange
        var sut = BuildSut(BuildTools(totalTools, failAtIndex));

        // Act
        var result = await sut.Transform(0);

        // Assert: reducer is called despite partial failure; failed tool excluded from sum
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(totalTools * (totalTools + 1) / 2 - failAtIndex);
    }

    [Fact]
    public async Task Transform_FailsWhenAllToolsFail()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<int, int>>();
        var tool2 = Substitute.For<ITool<int, int>>();
        tool1.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<int>("tool1 failed")));
        tool2.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<int>("tool2 failed")));
        var sut = ParallelTool
            .Add(tool1)
            .Add(tool2)
            .Reduce<int>(_ => Result.Ok(0));

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ThrowsWhenNoReducerDefined()
    {
        // Arrange
        var tool = Substitute.For<ITool<int, int>>();
        tool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(1)));
        var sut = ParallelTool.Add(tool);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Transform(0));
    }
}
