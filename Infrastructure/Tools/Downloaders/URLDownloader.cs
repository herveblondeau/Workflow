using Core;
using FluentResults;

namespace Infrastructure.Downloaders;

/// <summary>
/// Downloads content from a URL and returns it as a string
/// </summary>
public class URLDownloader : ITool<string, string>
{
    private readonly HttpClient _httpClient;

    public URLDownloader()
    {
        _httpClient = new HttpClient();
    }

    public async Task<Result<string>> Transform(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Fail($"{nameof(URLDownloader)}: URL is required");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result.Fail($"{nameof(URLDownloader)}: Invalid URL format");
        }

        try
        {
            var response = await _httpClient.GetAsync(uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Fail($"{nameof(URLDownloader)}: HTTP request failed with status {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Ok(content);
        }
        catch (HttpRequestException ex)
        {
            return Result.Fail(new Error($"{nameof(URLDownloader)}: HTTP request failed").CausedBy(ex));
        }
        catch (TaskCanceledException ex)
        {
            return Result.Fail(new Error($"{nameof(URLDownloader)}: Request was cancelled").CausedBy(ex));
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error($"{nameof(URLDownloader)}: Unexpected error occurred").CausedBy(ex));
        }
    }
}
