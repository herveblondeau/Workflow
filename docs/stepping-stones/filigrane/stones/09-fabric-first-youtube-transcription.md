# 9. Fabric-first YouTube transcription

**PR:** `#15`

- **Added:**
  - `Infrastructure/Processes/IProcessRunner.cs` + `ProcessRunner.cs` — a minimal seam over
    `System.Diagnostics.Process` (`Run(fileName, args, ct) -> (ExitCode, StandardOutput,
    StandardError)`), args passed as a list so each maps to one argv entry. Reads stdout/stderr
    concurrently before awaiting exit to avoid pipe-buffer deadlocks. Used only by the new tool
  - `Infrastructure/Tools/Downloaders/FabricTranscriptDownloader.cs` — `ITool<string, string>`,
    ctor `(string language, IProcessRunner? runner = null)`. Runs
    `fabric -y "{url}" --transcript --yt-dlp-args=--sub-langs {language}`, returns trimmed stdout
    on exit 0, else `Result.Fail` (so `FirstSuccessfulTool` falls through)
  - Wired as the **first** tier of the `FirstSuccessfulTool` chain in both call sites
    (`Main.Console/Presets/YouTubeSummary.cs`, `Main.Api/AnalysisController.cs`):
    `fabric -> subtitles -> audio+whisper`
- **Revealed:**
  - The "fabric is redundant with `YouTubeSubtitlesDownloader`" worry (raised while proposing this
    stone) was wrong. `YouTubeSubtitlesDownloader` runs `yt-dlp --write-subs`, which only fetches
    *manually-uploaded* subtitles, so it fails on most videos. Fabric grabs auto-generated
    captions too, so it succeeds on the majority and faster than audio+Whisper. They are genuinely
    complementary, so the old chain stays as real defense-in-depth (retracted the earlier idea of
    dropping the subtitles tool later)
  - Fabric's Go flag parser requires `--yt-dlp-args=VALUE` as a single argv entry with `=` syntax;
    the space-separated form (`--yt-dlp-args "--sub-langs en"`) is rejected. This is why the seam
    passes args as a list rather than one quoted string
- **Demo:**
  - `dotnet test --filter FullyQualifiedName~FabricTranscriptDownloader` (7 pass; pins the
    fallback contract + arg construction)
  - Raw tool, confirmed locally:
    `fabric -y "https://www.youtube.com/watch?v=jNQXAC9IVRw" --transcript "--yt-dlp-args=--sub-langs en"`
    returned a real transcript
  - End-to-end (API running + OpenRouter key): `POST /api/analysis/url` with a captions-only
    YouTube video returns a summary fast via fabric, where before it fell through to slow Whisper
- **Test:** `Tests/Infrastructure/FabricTranscriptDownloaderTests.cs`
