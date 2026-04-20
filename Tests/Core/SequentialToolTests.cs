using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class SequentialToolTests
{
    // Each tool adds its 1-based index to the input (tool2: x → x + 2).
    // With initial input 0, the output after N tools is 1+2+...+N = N*(N+1)/2.
    // Only the tool at failAtIndex fails; all other tools (including subsequent ones) succeed.
    // failAtIndex = 0 means all tools succeed.
    private static (ITool<int, int>[] tools, SequentialTool<int, int> sut) BuildSut(
        int totalTools, int failAtIndex = 0)
    {
        return Build(totalTools, oneBased => failAtIndex > 0 && oneBased == failAtIndex);
    }

    // Same as BuildSut, but all tools from failFromIndex onwards fail.
    // Used to verify that subsequent failures don't bleed into the result.
    private static (ITool<int, int>[] tools, SequentialTool<int, int> sut) BuildSutWithCascadingFailure(
        int totalTools, int failFromIndex)
    {
        return Build(totalTools, oneBased => oneBased >= failFromIndex);
    }

    private static (ITool<int, int>[] tools, SequentialTool<int, int> sut) Build(
        int totalTools, Func<int, bool> shouldFail)
    {
        var tools = Enumerable.Range(1, totalTools)
            .Select(_ => Substitute.For<ITool<int, int>>())
            .ToArray();

        for (int i = 0; i < totalTools; i++)
        {
            int oneBased = i + 1;
            if (shouldFail(oneBased))
            {
                tools[i].Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(Result.Fail<int>($"tool{oneBased} error")));
            }
            else
            {
                tools[i].Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo => Task.FromResult(Result.Ok(callInfo.ArgAt<int>(0) + oneBased)));
            }
        }

        SequentialTool<int, int> sut = SequentialTool.Add(tools[0]);
        foreach (var tool in tools.Skip(1))
        {
            sut = sut.Add(tool);
        }

        return (tools, sut);
    }

    [Theory]
    [InlineData(1)] // result = 1
    [InlineData(2)] // result = 3
    [InlineData(3)] // result = 6
    [InlineData(5)] // result = 15
    public async Task Transform_ChainsToolsAndReturnsLastOutput(int totalTools)
    {
        // Arrange
        var (_, sut) = BuildSut(totalTools);

        // Act
        var result = await sut.Transform(0);

        // Assert: result is 1+2+...+N, proving every tool in the chain ran
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(totalTools * (totalTools + 1) / 2);
    }

    [Theory]
    [InlineData(2, 1)] // first fails, second skipped
    [InlineData(2, 2)] // first succeeds, second fails (no tools to skip)
    [InlineData(3, 1)] // first fails, second and third skipped
    [InlineData(3, 2)] // first succeeds, second fails, third skipped
    [InlineData(5, 3)] // first two succeed, third fails, fourth and fifth skipped
    public async Task Transform_StopsOnFirstFailureAndSkipsRemainingTools(int totalTools, int failAtIndex)
    {
        // Arrange
        var (tools, sut) = BuildSut(totalTools, failAtIndex);

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.IsFailed.Should().BeTrue();
        foreach (var tool in tools.Skip(failAtIndex))
        {
            await tool.DidNotReceive().Transform(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(2, 1)] // first tool fails
    [InlineData(2, 2)] // second tool fails
    [InlineData(3, 2)] // second tool fails
    [InlineData(5, 3)] // third tool fails
    public async Task Transform_PropagatesErrorFromFailedTool(int totalTools, int failAtIndex)
    {
        // Arrange
        var (_, sut) = BuildSut(totalTools, failAtIndex);

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.Errors.Should().ContainSingle(e => e.Message.Contains($"tool{failAtIndex} error"));
    }

    [Theory]
    [InlineData(2, 1)] // all tools fail, only first error propagates
    [InlineData(3, 1)] // all tools fail, only first error propagates
    [InlineData(3, 2)] // first succeeds, last two fail, only second's error propagates
    [InlineData(5, 3)] // first two succeed, last three fail, only third's error propagates
    public async Task Transform_PropagatesOnlyFirstError_WhenSubsequentToolsWouldAlsoFail(int totalTools, int failFromIndex)
    {
        // Arrange
        var (_, sut) = BuildSutWithCascadingFailure(totalTools, failFromIndex);

        // Act
        var result = await sut.Transform(0);

        // Assert: only the error from the first failing tool propagates
        result.Errors.Should().ContainSingle(e => e.Message.Contains($"tool{failFromIndex} error"));
    }

    [Theory]
    [InlineData(2)] // tool[1] must receive tool[0]'s output
    [InlineData(3)] // tool[i] must receive the cumulative sum from all previous tools
    [InlineData(5)]
    public async Task Transform_PassesIntermediateOutputToNextTool(int totalTools)
    {
        // Arrange
        var (tools, sut) = BuildSut(totalTools);

        // Act
        await sut.Transform(0);

        // Assert: tool[i] (0-based) receives i*(i+1)/2, the cumulative sum of all previous additions
        for (int i = 0; i < totalTools; i++)
            await tools[i].Received(1).Transform(i * (i + 1) / 2, Arg.Any<CancellationToken>());
    }
}
