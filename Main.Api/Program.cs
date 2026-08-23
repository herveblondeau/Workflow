using Main.Api;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.Providers;
using Infrastructure.Tools.Watermarking;
using Main.Api.Watermarking;
using Main.Api.Watermarking.Storage;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddTransient<IProviderModelSource, AnthropicModelSource>();
builder.Services.AddTransient<IProviderModelSource, OpenAIModelSource>();
builder.Services.AddTransient<IProviderModelSource, GeminiModelSource>();
builder.Services.AddTransient<IProviderModelSource, OpenRouterModelSource>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Filigrane-compatible watermark endpoints (stone 1)
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<IFileStore, LocalFileStore>();
builder.Services.AddSingleton<PdfWatermarker>();
builder.Services.AddHostedService<CleanupService>();
builder.Services.AddSingleton<DiskWatermarkPipeline>();
builder.Services.AddSingleton<StreamingWatermarkPipeline>();
builder.Services.AddSingleton<IWatermarkPipeline, RoutingWatermarkPipeline>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

var app = builder.Build();

if (string.IsNullOrEmpty(app.Configuration["API_KEY"]))
    throw new InvalidOperationException("API_KEY is not configured.");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireAuthorization();

app.Run();
