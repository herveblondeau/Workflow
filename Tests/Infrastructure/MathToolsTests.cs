using AwesomeAssertions;
using Core.Models;
using Infrastructure.MathTools;

namespace Tests.Infrastructure;

public class MathToolsTests
{
    [Fact]
    public async Task AdditionTool_ReturnsOkWithSum()
    {
        // Arrange
        var sut = new AdditionTool();

        // Act
        var result = await sut.Transform(new BinaryMathInput(2, 3));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task SubtractionTool_ReturnsOkWithDifference()
    {
        // Arrange
        var sut = new SubtractionTool();

        // Act
        var result = await sut.Transform(new BinaryMathInput(10, 4));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(6);
    }

    [Fact]
    public async Task MultiplicationTool_ReturnsOkWithProduct()
    {
        // Arrange
        var sut = new MultiplicationTool();

        // Act
        var result = await sut.Transform(new BinaryMathInput(6, 7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task DivisionTool_ReturnsOkWithQuotient()
    {
        // Arrange
        var sut = new DivisionTool();

        // Act
        var result = await sut.Transform(new BinaryMathInput(20, 5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(4);
    }

    [Fact]
    public async Task DivisionTool_ReturnsFailWhenDividingByZero()
    {
        // Arrange
        var sut = new DivisionTool();

        // Act
        var result = await sut.Transform(new BinaryMathInput(1, 0));

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("DivisionTool"));
    }
}
