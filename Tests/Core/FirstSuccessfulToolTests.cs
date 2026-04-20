using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class FirstSuccessfulToolTests
{
    // Tools before firstSuccessIndex fail with "tool{i} error".
    // Tool at firstSuccessIndex and all after it return "tool{i}".
    // firstSuccessIndex = 0 means all tools fail.
    private static (ITool<string, string>[] tools, FirstSuccessfulTool<string, string> sut) BuildSut(
        int totalTools, int firstSuccessIndex = 0)
    {
        var tools = Enumerable.Range(1, totalTools)
            .Select(_ => Substitute.For<ITool<string, string>>())
            .ToArray();

        for (int i = 0; i < totalTools; i++)
        {
            int oneBased = i + 1;
            bool succeeds = firstSuccessIndex > 0 && oneBased >= firstSuccessIndex;
            tools[i].Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(succeeds
                    ? Result.Ok($"tool{oneBased}")
                    : Result.Fail<string>($"tool{oneBased} error")));
        }

        var sut = tools.Skip(1).Aggregate(
            FirstSuccessfulTool.Add(tools[0]),
            (acc, tool) => acc.Add(tool));

        return (tools, sut);
    }

    [Theory]
    [InlineData(1, 1)] // only tool succeeds
    [InlineData(2, 1)] // first tool succeeds, second skipped
    [InlineData(3, 1)] // first tool succeeds, second and third skipped
    [InlineData(5, 1)] // first tool succeeds, all subsequent skipped
    [InlineData(5, 2)] // second tool succeeds, all subsequent skipped
    [InlineData(5, 3)] // third tool succeeds, all subsequent skipped
    [InlineData(5, 4)] // fourth tool succeeds, fifth
    [InlineData(5, 5)] // fifth tool succeeds
    public async Task Transform_ReturnsFirstSuccessfulResult(int totalTools, int firstSuccessIndex)
    {
        // Arrange
        var (tools, sut) = BuildSut(totalTools, firstSuccessIndex);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be($"tool{firstSuccessIndex}");
        foreach (var tool in tools.Skip(firstSuccessIndex))
            await tool.DidNotReceive().Transform(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2, 2)] // first fails, second succeeds
    [InlineData(3, 2)] // first fails, second succeeds, third skipped
    [InlineData(3, 3)] // first two fail, third succeeds
    [InlineData(5, 4)] // first three fail, fourth succeeds
    public async Task Transform_SkipsFailingToolsAndReturnsFirstSuccess(int totalTools, int firstSuccessIndex)
    {
        // Arrange
        var (tools, sut) = BuildSut(totalTools, firstSuccessIndex);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be($"tool{firstSuccessIndex}");
        foreach (var tool in tools.Skip(firstSuccessIndex))
            await tool.DidNotReceive().Transform(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public async Task Transform_FailsWhenAllToolsFail(int totalTools)
    {
        // Arrange
        var (_, sut) = BuildSut(totalTools);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains($"All {totalTools} tools failed"));
    }

    [Theory]
    [InlineData(2, 2)] // first fails, its reason appears in the success result
    [InlineData(3, 2)] // first fails, third skipped; first's reason appears in the success result
    [InlineData(3, 3)] // first two fail, both their reasons appear in the success result
    [InlineData(6, 4)] // first three fail, all their reasons appear in the success result
    public async Task Transform_IncludesPreviousFailureReasonsInSuccessfulResult(int totalTools, int firstSuccessIndex)
    {
        // Arrange
        var (_, sut) = BuildSut(totalTools, firstSuccessIndex);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        for (int i = 1; i < firstSuccessIndex; i++)
            result.Successes.Should().Contain(s => s.Message.Contains($"tool{i} error"));
    }
}
