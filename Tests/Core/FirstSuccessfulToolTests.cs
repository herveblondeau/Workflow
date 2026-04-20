using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class FirstSuccessfulToolTests
{
    [Fact]
    public async Task Transform_ReturnsFirstSuccessfulResult()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("from tool1")));
        var sut = FirstSuccessfulTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("from tool1");
        await tool2.DidNotReceive().Transform(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_SkipsFailingToolsAndReturnsFirstSuccess()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool1 failed")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("from tool2")));
        var sut = FirstSuccessfulTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("from tool2");
    }

    [Fact]
    public async Task Transform_FailsWhenAllToolsFail()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool1 error")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool2 error")));
        var sut = FirstSuccessfulTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("All 2 tools failed"));
    }

    [Fact]
    public async Task Transform_IncludesPreviousFailureReasonsInSuccessfulResult()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool1 error")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("value")));
        var sut = FirstSuccessfulTool.Add(tool1).Add(tool2);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Successes.Should().Contain(s => s.Message.Contains("tool1 error"));
    }
}
