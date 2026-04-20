using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class SequentialToolTests
{
    [Fact]
    public async Task Transform_ChainsToolsAndReturnsLastOutput()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, int>>();
        var tool2 = Substitute.For<ITool<int, bool>>();
        tool1.Transform("input", Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Ok(42)));
        tool2.Transform(42, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Ok(true)));
        var sut = SequentialTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_StopsOnFirstFailureAndSkipsRemainingTools()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, int>>();
        var tool2 = Substitute.For<ITool<int, bool>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<int>("tool1 failed")));
        var sut = SequentialTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsFailed.Should().BeTrue();
        await tool2.DidNotReceive().Transform(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_PropagatesErrorFromFailedTool()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, int>>();
        var tool2 = Substitute.For<ITool<int, bool>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<int>("specific error")));
        var sut = SequentialTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.Errors.Should().Contain(e => e.Message == "specific error");
    }

    [Fact]
    public async Task Transform_PassesIntermediateOutputToNextTool()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, int>>();
        var tool2 = Substitute.For<ITool<int, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(99)));
        tool2.Transform(99, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("done")));
        var sut = SequentialTool.Add(tool1).Add(tool2);

        // Act
        await sut.Transform("anything");

        // Assert
        await tool2.Received(1).Transform(99, Arg.Any<CancellationToken>());
    }
}
