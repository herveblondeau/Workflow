# Workflow

A composable, pipeline-based content processing framework for .NET. Chain tools together to build workflows that download, record, transcribe, and transform content.

Example workflows:

- **YouTube URL → AI analysis**: download audio from a YouTube video, transcribe it with Whisper, and send the transcript to an AI model for summarization or Q&A
- **Live meeting → meeting minutes**: record microphone and system audio simultaneously, transcribe the recording, and generate structured notes (decisions, action items, participants)
- **Image → translated text**: read an image file, extract text with Tesseract OCR, and use an AI model to translate or answer questions about the content
- **Web page → summary**: fetch the content of any URL and pass it to an AI model for analysis

## Architecture

The solution follows a clean architecture split into four projects:

| Project          | Role                                                                      |
| ---------------- | ------------------------------------------------------------------------- |
| `Core`           | Abstractions: `ITool<TIn,TOut>`, `IWorkflow<TIn,TOut>`, composition tools |
| `Infrastructure` | Concrete tool implementations (recorders, downloaders, transcribers, AI)  |
| `Main.Console`   | Console entry point with ready-made presets                               |
| `Main.Api`       | REST API entry point                                                      |

## Core Concepts

### Tool

A tool is the basic processing unit. It transforms an input into an output:

```csharp
public interface ITool<TIn, TOut>
{
    Task<Result<TOut>> Transform(TIn input, CancellationToken cancellationToken = default);
}
```

All results use [FluentResults](https://github.com/altmann/FluentResults) for explicit success/failure handling.

### Workflow

Workflows are built by chaining tools together using a fluent builder:

```csharp
var workflow = Workflow
    .Add(new YouTubeAudioDownloader(audioFormat))
    .Add(new WhisperTranscriber(audioFormat, "en", GgmlType.Base))
    .Add(new AITextTransformer(chatAgent, "en", new[] { "Can you write a summary?" }));

var result = await workflow.Execute(youtubeUrl, cancellationToken);
```

### Composition Tools

Four built-in tools allow building complex branching logic:

| Tool                  | Behaviour                                              |
| --------------------- | ------------------------------------------------------ |
| `SequentialTool`      | Runs tools one after the other; stops on first failure |
| `ParallelTool`        | Runs tools concurrently; merges results via a reducer  |
| `ConditionalTool`     | Routes input to one of two tools based on a predicate  |
| `FirstSuccessfulTool` | Tries tools in order; returns the first success        |

**SequentialTool**

```csharp
var tool = SequentialTool
    .Add(new DownloadTool())
    .Add(new TranscribeTool())
    .Add(new SummarizeTool());
```

**ParallelTool**

```csharp
var tool = ParallelTool
    .Add(new ToolA())   // ITool<string, int>
    .Add(new ToolB())   // ITool<string, int>
    .Reduce((results, ct) =>
        Task.FromResult(Result.Ok(results.OfType<Result<int>>().Sum(r => r.Value))));
```

**ConditionalTool**

```csharp
var tool = ConditionalTool.If(
    condition: input => input.Contains("youtube"),
    thenTool: new YouTubeWorkflow(),
    elseTool: new GenericUrlWorkflow());
```

**FirstSuccessfulTool**

```csharp
var tool = FirstSuccessfulTool
    .Add(new YouTubeSubtitlesDownloader(language))  // fast path
    .Add(SequentialTool                              // fallback
        .Add(new YouTubeAudioDownloader(audioFormat))
        .Add(new WhisperTranscriber(audioFormat, language, GgmlType.Base)));
```

## Infrastructure Tools

### Recorders

Record audio from system sources. Cross-platform support for Windows and Linux.

| Class                       | Description                                  |
| --------------------------- | -------------------------------------------- |
| `WindowsAudioRecorder`      | Records system audio (Windows)               |
| `WindowsMicrophoneRecorder` | Records microphone (Windows)                 |
| `LinuxAudioRecorder`        | Records system audio (Linux)                 |
| `LinuxMicrophoneRecorder`   | Records microphone (Linux)                   |
| `MultiSourceRecorder`       | Mixes multiple audio sources into one stream |

### Downloaders

| Class                        | Input       | Output                  | Requires              |
| ---------------------------- | ----------- | ----------------------- | --------------------- |
| `YouTubeAudioDownloader`     | YouTube URL | `AudioStream`           | `yt-dlp`, `ffmpeg`    |
| `YouTubeSubtitlesDownloader` | YouTube URL | `string` (transcript)   | `yt-dlp`              |
| `URLDownloader`              | URL         | `string` (page content) |                       |

### Transcribers

| Class                     | Input         | Output                                    | Requires      |
| ------------------------- | ------------- | ----------------------------------------- | ------------- |
| `WhisperTranscriber`      | `AudioStream` | `string` (speech-to-text via Whisper.net) |               |
| `TesseractOcrTranscriber` | `ImageStream` | `string` (OCR via Tesseract)              | `tesseract`   |

### Text Transformers

| Class               | Description                                                             |
| ------------------- | ----------------------------------------------------------------------- |
| `AITextTransformer` | Sends text to an AI model via OpenRouter with configurable instructions |

### File Readers

| Class             | Input                | Output        |
| ----------------- | -------------------- | ------------- |
| `AudioFileReader` | `string` (file path) | `AudioStream` |
| `ImageFileReader` | `string` (file path) | `ImageStream` |

## Configuration

Set the OpenRouter API key in `appsettings.json` or via user secrets:

```json
{
  "ChatClients": {
    "OpenRouter": {
      "ApiKey": "your-api-key-here"
    }
  }
}
```

The `DOTNET_ENVIRONMENT` environment variable controls which `appsettings.{env}.json` is loaded (`Development`, `Production`, etc.).

## REST API (`Main.Api`)

Base path: `/api/analysis`

### `POST /api/analysis/text`

Transforms raw text using AI.

```json
{
  "text": "The text to process",
  "language": "en",
  "instructions": "Summarize in bullet points\nKeep it under 100 words"
}
```

### `POST /api/analysis/image`

Runs OCR on an uploaded image, then applies AI transformation. Accepts `multipart/form-data`.

| Field      | Type          | Description                                   |
| ---------- | ------------- | --------------------------------------------- |
| `image`    | file          | Image file                                    |
| `metadata` | string (JSON) | `{ "language": "en", "instructions": "..." }` |

### `POST /api/analysis/url`

Fetches and analyzes content from a URL. Automatically switches to the YouTube pipeline when the URL contains `youtube`.

```json
{
  "text": "https://www.youtube.com/watch?v=...",
  "language": "en",
  "instructions": "Can you write a summary?"
}
```

All endpoints return:

```json
{ "success": true, "result": "..." }
```

## Console Presets (`Main.Console`)

This project contains utility classes that perform predefined workflows.

### `YouTubeSummary`

Downloads, transcribes, and summarizes a YouTube video. Falls back to audio transcription if subtitles are unavailable.

```csharp
var summary = new YouTubeSummary(openRouterApiKey);
var result = await summary.Summarize("https://www.youtube.com/watch?v=...", "en");
```

### `MeetingMinutes`

Records microphone and system audio simultaneously, transcribes, and produces structured meeting notes.

```csharp
var minutes = new MeetingMinutes(openRouterApiKey, async (ct) =>
{
    Console.ReadLine(); // press Enter to stop recording
});
var result = await minutes.Summarize("en", MeetingMinutes.MeetingType.Business);
```

Supported meeting types: `Business`, `Interview`, `Workshop`.

## Dependencies

- [FluentResults](https://github.com/altmann/FluentResults) — result/error handling
- [Whisper.net](https://github.com/sandrohanea/whisper.net) — local speech-to-text (Whisper models)
- [Tesseract](https://github.com/charlesw/tesseract) — OCR
- [OpenRouter](https://openrouter.ai) — AI model gateway (default model: `google/gemini-2.5-flash-image`)
