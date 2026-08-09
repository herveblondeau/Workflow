# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Workflow is a composable, pipeline-based content processing framework for .NET (net10.0). Tools chain together to download, record, transcribe, and transform content (audio/image/text/URL) — e.g. YouTube → transcript → AI summary, or live meeting audio → transcript → structured minutes.

## Commands

```bash
dotnet build                                   # build entire solution
dotnet test                                    # run all tests (xunit)
dotnet test --filter "FullyQualifiedName~ParallelToolTests"   # run a single test class
dotnet test --filter "DisplayName~SomeTestName"                # run a single test

dotnet run --project Main.Api                  # run REST API
dotnet run --project Main.Console              # run console presets
```

Tests live in `Tests/`, mirroring the `Core`/`Infrastructure` project layout (e.g. `Tests/Core/ParallelToolTests.cs`). Mocking uses NSubstitute; assertions use AwesomeAssertions.

`TreatWarningsAsErrors` is set solution-wide in `Directory.Build.props` — builds fail on warnings.

## Architecture

Four projects, clean-architecture style:

| Project | Role |
| --- | --- |
| `Core` | Abstractions only: `ITool<TIn,TOut>`, `IWorkflow<TIn,TOut>`, the composition tools. No external dependencies besides FluentResults. |
| `Infrastructure` | Concrete tool implementations — recorders, downloaders, transcribers, AI chat agents/providers. References `Core`. |
| `Main.Console` | Console entry point with ready-made presets (`Main.Console/Presets/`). References `Infrastructure`. |
| `Main.Api` | ASP.NET Core REST API entry point. References `Core` + `Infrastructure`. |

### Tool / Workflow model

The entire system is built on one interface:

```csharp
public interface ITool<TIn, TOut>
{
    Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default);
}
```

All results use `FluentResults.Result<T>` for explicit success/failure — there's no throw-based control flow between tools. `Workflow<TIn,TOut>` (`Core/IWorkflow.cs`) chains tools with `.Add(...)`, short-circuiting to the accumulated failure if any step fails. `Unit` stands in for tools that take no input.

Four composition tools in `Core/Tools/` let you build branching pipelines out of ordinary tools instead of writing bespoke control flow:

- `SequentialTool` — runs in order, stops on first failure
- `ParallelTool` — runs concurrently, merges results via a supplied reducer
- `ConditionalTool` — routes to one of two tools based on a predicate
- `FirstSuccessfulTool` — tries tools in order, returns the first success (used for fast-path/fallback pipelines, e.g. YouTube subtitles vs. audio+Whisper transcription)

When adding a new capability, implement it as an `ITool<TIn, TOut>` in `Infrastructure` and compose it into a workflow rather than adding bespoke orchestration logic.

### Infrastructure layout

- `ChatAgents/` — `IChatAgent`/`ChatAgent` wraps `Microsoft.Extensions.AI` chat clients; `ChatClientFactory` builds a client per provider given a `ProviderModel`; `Providers/` holds one `IProviderModelSource` per AI provider (Anthropic, OpenAI, Gemini, OpenRouter) responsible for listing that provider's available models.
- `Tools/Downloaders/` — YouTube audio/subtitles, generic URL content.
- `Tools/Recorders/` — platform-specific audio capture (Windows/Linux, mic/system audio), plus `MultiSourceRecorder` to mix sources.
- `Tools/Transcribers/` — Whisper.net (local speech-to-text) and Tesseract (OCR).
- `Tools/TextTransformers/` — `AITextTransformer` sends text to a chat agent with instructions.
- `Files/` — file-based readers (`AudioFileReader`, `ImageFileReader`) that produce `AudioStream`/`ImageStream` models for the rest of the pipeline.

Platform-specific recorders mean some tools only work on Windows or Linux — check which recorder is being instantiated when debugging recording issues.

### Main.Api

- `Program.cs` wires up config loading and DI. Config precedence: `.env` file → `appsettings.json`/`appsettings.{env}.json` → .NET user secrets. `DOTNET_ENVIRONMENT` selects which `appsettings.{env}.json` loads.
- `ApiKeyAuthenticationHandler` enforces the `X-Api-Key` header (matched against `API_KEY`) on every endpoint except `GET /api/system/status`. The app refuses to start if `API_KEY` is unset.
- `SystemController` — health check + `GET /api/system/models` (lists models per configured provider; providers with missing keys or failing calls are silently omitted).
- `AnalysisController` — `POST /api/analysis/text`, `/image` (multipart, runs OCR then AI), `/url` (auto-switches to the YouTube pipeline when the URL contains `youtube`).
- Required/optional env vars (`API_KEY`, `{PROVIDER}_API_KEY`, `{PROVIDER}_DEFAULT_MODEL` for openai/anthropic/gemini/openrouter) are documented with examples in `README.md`.

### Main.Console

`Presets/` contains full end-to-end workflows assembled from `Infrastructure` tools (e.g. `YouTubeSummary`, `MeetingMinutes`). Use these as the reference pattern when wiring a new preset — construct the tools, chain them via `Workflow`/composition tools, and expose a single async entry method.
