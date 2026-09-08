# 7. Install external tools (tesseract, ffmpeg, yt-dlp) in the Docker image

**Commit:** (this commit)

- **Added:**
  - `Main.Api/Dockerfile` runtime stage now installs `tesseract-ocr` (+ `eng`/`fra`/`jpn`
    language data, matching the languages `TesseractOcrTranscriber` supports) and `ffmpeg`
    via apt, and downloads the latest `yt-dlp` standalone binary release from GitHub
    (arch-selected via `TARGETARCH`, amd64/arm64) to `/usr/local/bin/yt-dlp`
  - yt-dlp is pulled unpinned (`.../releases/latest/download/...`) rather than pinned to a
    version: YouTube changes regularly and a stale pinned yt-dlp is more likely to silently
    break than an unpinned one; agreed with the user as the intentional trade-off (build
    reproducibility for staying working against YouTube)
- **Revealed:**
  - Whisper needed no Dockerfile change at all: `WhisperTranscriber` uses the
    `Whisper.net`/`Whisper.net.Runtime` NuGet package (native lib bundled at publish time,
    model downloaded to a temp dir at first use), not an external CLI. Only tesseract,
    ffmpeg and yt-dlp are actually shelled out to via `Process.Start` from code reachable
    by `Main.Api`.
  - yt-dlp's YouTube extraction currently emits a warning about no JS runtime being
    available (only `deno` is checked for by default) and recommends one for full format
    support; extraction still succeeded in testing (title, subtitles) without it. Not
    acted on now — logged here in case a future yt-dlp/YouTube change makes it necessary.
- **Demo:**
  - `podman build -f Main.Api/Dockerfile -t workflow-api-test .` (podman used in place of
    docker in the build sandbox; docker daemon unavailable there, same as stone 3)
  - `podman run --rm --entrypoint sh workflow-api-test -c 'tesseract --version; ffmpeg -version; yt-dlp --version; tesseract --list-langs'`
    confirms all three tools and the `eng`/`fra`/`jpn` tessdata are present
  - Ran the container with `API_KEY` set and hit `POST /api/analysis/image` with a
    generated PNG containing text ("Hello World") — request reached the AI step (failed
    there only on "OpenAI model is not configured", i.e. OCR itself succeeded)
  - Ran `yt-dlp --skip-download --write-subs --sub-langs en -o subtest <youtube-url>`
    inside the container (the exact invocation shape `YouTubeSubtitlesDownloader` uses) —
    downloaded a real `.vtt` subtitles file end to end
- **Test:** infra-only change, no new application logic to pin (matches stones 3/4) —
  verified via the podman build + manual demo above
