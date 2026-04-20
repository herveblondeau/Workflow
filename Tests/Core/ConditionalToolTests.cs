using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class ConditionalToolTests
{
    [Fact]
    public async Task Transform_WhenConditionIsTrue_RunsThenTool()
    {
        // Arrange
        var thenTool = Substitute.For<ITool<int, string>>();
        var elseTool = Substitute.For<ITool<int, string>>();
        thenTool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("from then")));
        var sut = ConditionalTool.If(n => n > 0, thenTool, elseTool);

        // Act
        var result = await sut.Transform(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("from then");
        await thenTool.Received(1).Transform(5, Arg.Any<CancellationToken>());
        await elseTool.DidNotReceive().Transform(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_WhenConditionIsFalse_RunsElseTool()
    {
        // Arrange
        var thenTool = Substitute.For<ITool<int, string>>();
        var elseTool = Substitute.For<ITool<int, string>>();
        elseTool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("from else")));
        var sut = ConditionalTool.If(n => n > 0, thenTool, elseTool);

        // Act
        var result = await sut.Transform(-1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("from else");
        await elseTool.Received(1).Transform(-1, Arg.Any<CancellationToken>());
        await thenTool.DidNotReceive().Transform(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_WhenConditionIsTrue_AddsConditionFulfilledToSuccessReasons()
    {
        // Arrange
        var thenTool = Substitute.For<ITool<int, string>>();
        var elseTool = Substitute.For<ITool<int, string>>();
        thenTool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("value")));
        var sut = ConditionalTool.If(_ => true, thenTool, elseTool);

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.Successes.Should().Contain(s => s.Message.Contains("condition fulfilled"));
    }

    [Fact]
    public async Task Transform_WhenConditionIsFalse_AddsConditionNotFulfilledToSuccessReasons()
    {
        // Arrange
        var thenTool = Substitute.For<ITool<int, string>>();
        var elseTool = Substitute.For<ITool<int, string>>();
        elseTool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("value")));
        var sut = ConditionalTool.If(_ => false, thenTool, elseTool);

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.Successes.Should().Contain(s => s.Message.Contains("condition not fulfilled"));
    }

    [Fact]
    public async Task Transform_WhenSelectedToolFails_PropagatesFailure()
    {
        // Arrange
        var thenTool = Substitute.For<ITool<int, string>>();
        var elseTool = Substitute.For<ITool<int, string>>();
        thenTool.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("thenTool failed")));
        var sut = ConditionalTool.If(_ => true, thenTool, elseTool);

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message == "thenTool failed");
    }
}
