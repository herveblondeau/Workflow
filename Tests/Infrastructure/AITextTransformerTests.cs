using AwesomeAssertions;
using Core.ChatAgents;
using Infrastructure.TextTransformers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Tests.Infrastructure;

public class AITextTransformerTests
{
    [Fact]
    public async Task Transform_ReturnsOkWithAgentResponse()
    {
        // Arrange
        var chatAgent = Substitute.For<IChatAgent>();
        chatAgent.Prompt(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("processed text"));
        var sut = new AITextTransformer(chatAgent, "English", ["Do something"]);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("processed text");
    }

    [Fact]
    public async Task Transform_IncludesLanguageAndInstructionsInPrompt()
    {
        // Arrange
        var chatAgent = Substitute.For<IChatAgent>();
        chatAgent.Prompt(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("ok"));
        var sut = new AITextTransformer(chatAgent, "French", ["Summarize", "Be concise"]);

        // Act
        await sut.Transform("some content");

        // Assert
        await chatAgent.Received(1).Prompt(
            Arg.Is<string>(p =>
                p.Contains("French") &&
                p.Contains("- Summarize") &&
                p.Contains("- Be concise") &&
                p.Contains("some content")),
            Arg.Any<bool>()
        );
    }

    [Fact]
    public async Task Transform_ReturnsFailWhenAgentThrows()
    {
        // Arrange
        var chatAgent = Substitute.For<IChatAgent>();
        chatAgent.Prompt(Arg.Any<string>(), Arg.Any<bool>()).ThrowsAsync(new Exception("network error"));
        var sut = new AITextTransformer(chatAgent, "English", ["Do something"]);

        // Act
        var result = await sut.Transform("input");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("AITextTransformer"));
    }

    [Fact]
    public async Task Transform_InitializesConversationBeforeEachCall()
    {
        // Arrange
        var chatAgent = Substitute.For<IChatAgent>();
        chatAgent.Prompt(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult("response"));
        var sut = new AITextTransformer(chatAgent, "English", []);

        // Act
        await sut.Transform("first");
        await sut.Transform("second");

        // Assert
        chatAgent.Received(2).InitializeConversation();
    }
}
