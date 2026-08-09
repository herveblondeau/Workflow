# Adding a new AI provider

Two separate wiring points, depending on what the provider needs to support:

**Model listing** (`GET /api/system/models`): implement `IProviderModelSource` — `ProviderId`, `ProviderLabel`, and `GetModelsAsync` returning `null` on a missing key or failed call (never throw; `SystemController` silently omits providers that return `null`). Register it in `Main.Api/Program.cs` alongside the existing `AddTransient<IProviderModelSource, ...>()` calls. Reference: `AnthropicModelSource.cs`.

**Chat capability**: add a case to the switch in `ChatClientFactory.Create` (`Infrastructure/ChatAgents/ChatClientFactory.cs`) that builds an `IChatClient` for the provider.

Both read config via the `{PROVIDER}_API_KEY` / `{PROVIDER}_DEFAULT_MODEL` naming convention (e.g. `ANTHROPIC_API_KEY`, `ANTHROPIC_DEFAULT_MODEL`) — keep new providers consistent with this so `.env`/`appsettings`/user-secrets loading in `Main.Api/Program.cs` picks them up without special-casing. Document the new env vars in `README.md`.
