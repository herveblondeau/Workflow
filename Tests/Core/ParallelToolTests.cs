using AwesomeAssertions;
using Core;
using FluentResults;
using NSubstitute;

namespace Tests.Core;

public class ParallelToolTests
{
    [Fact]
    public async Task Transform_RunsAllToolsAndPassesResultsToReducer()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("a")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("b")));
        IReadOnlyList<object>? captured = null;
        var sut = ParallelTool
            .Add(tool1)
            .Add(tool2)
            .Reduce<string>(results =>
            {
                captured = results;
                var values = results.OfType<Result<string>>().Where(r => r.IsSuccess).Select(r => r.Value);
                return Result.Ok(string.Join(",", values));
            });

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        captured.Should().HaveCount(2);
        await tool1.Received(1).Transform("input", Arg.Any<CancellationToken>());
        await tool2.Received(1).Transform("input", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transform_ReducerReceivesResultsFromAllTools()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<int, int>>();
        var tool2 = Substitute.For<ITool<int, int>>();
        tool1.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(10)));
        tool2.Transform(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok(20)));
        var sut = ParallelTool
            .Add(tool1)
            .Add(tool2)
            .Reduce<int>(results =>
            {
                var sum = results.OfType<Result<int>>().Where(r => r.IsSuccess).Sum(r => r.Value);
                return Result.Ok(sum);
            });

        // Act
        var result = await sut.Transform(0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(30);
    }

    [Fact]
    public async Task Transform_CallsReducerWithAllResultsWhenOneToolFails()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool1 failed")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("b")));
        var sut = ParallelTool
            .Add(tool1)
            .Add(tool2)
            .Reduce<string>(results =>
            {
                var values = results.OfType<Result<string>>().Where(r => r.IsSuccess).Select(r => r.Value);
                return Result.Ok(string.Join(",", values));
            });

        // Act
        var result = await sut.Transform("input");

        // Assert: reducer is called, partial failure doesn't abort execution
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("b");
    }

    [Fact]
    public async Task Transform_FailsWhenAllToolsFail()
    {
        // Arrange
        var tool1 = Substitute.For<ITool<string, string>>();
        var tool2 = Substitute.For<ITool<string, string>>();
        tool1.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool1 failed")));
        tool2.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Fail<string>("tool2 failed")));
        var sut = ParallelTool
            .Add(tool1)
            .Add(tool2)
            .Reduce<string>(_ => Result.Ok("unreachable"));

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ThrowsWhenNoReducerDefined()
    {
        // Arrange
        var tool = Substitute.For<ITool<string, string>>();
        tool.Transform(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Ok("value")));
        var sut = ParallelTool.Add(tool);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Transform("input"));
    }
}
