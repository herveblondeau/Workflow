using DotNetEnv;
using Infrastructure.ChatAgents;
using Infrastructure.ChatAgents.Providers;
using Main.Api;
using Main.Api.Filigrane.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// --- Filigrane (watermarking) ---
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<IFileStore, LocalFileStore>();
builder.Services.AddHostedService<CleanupService>();

// --- Existing services ---
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddTransient<IProviderModelSource, AnthropicModelSource>();
builder.Services.AddTransient<IProviderModelSource, OpenAIModelSource>();
builder.Services.AddTransient<IProviderModelSource, GeminiModelSource>();
builder.Services.AddTransient<IProviderModelSource, OpenRouterModelSource>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

// In prod, sits behind whatever reverse proxy the operator hosting this API
// runs (nginx, Caddy, etc.), which terminates TLS and forwards plain HTTP.
// Trust its X-Forwarded-* headers rather than relying on a fixed known proxy IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddAuthorization();
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

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireAuthorization();

app.Run();
