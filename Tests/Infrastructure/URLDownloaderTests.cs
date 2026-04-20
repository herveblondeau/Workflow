using System.Net;
using AwesomeAssertions;
using Infrastructure.Downloaders;

namespace Tests.Infrastructure;

public class URLDownloaderTests
{
    private static URLDownloader CreateWithResponse(HttpStatusCode statusCode, string content = "")
    {
        var handler = new StubHttpMessageHandler(statusCode, content);
        return new URLDownloader(new HttpClient(handler));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Transform_ReturnsFailForNullOrWhitespaceUrl(string? url)
    {
        // Arrange
        var sut = new URLDownloader(new HttpClient());

        // Act
        var result = await sut.Transform(url!);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Transform_ReturnsFailForInvalidUrl()
    {
        // Arrange
        var sut = new URLDownloader(new HttpClient());

        // Act
        var result = await sut.Transform("not-a-url");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Invalid URL format"));
    }

    [Fact]
    public async Task Transform_ReturnsSuccessWithBodyContent()
    {
        // Arrange
        var sut = CreateWithResponse(HttpStatusCode.OK, "page content");

        // Act
        var result = await sut.Transform("https://example.com");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("page content");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Transform_ReturnsFailOnHttpErrorStatus(HttpStatusCode statusCode)
    {
        // Arrange
        var sut = CreateWithResponse(statusCode);

        // Act
        var result = await sut.Transform("https://example.com");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("HTTP request failed"));
    }
}

file sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content = "") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
}
