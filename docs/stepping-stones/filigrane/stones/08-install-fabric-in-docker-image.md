# 8. Install fabric CLI in the Docker image

**Commit:** (this commit)

- **Added:**
  - `Main.Api/Dockerfile` runtime stage installs fabric (https://github.com/danielmiessler/fabric)
    via its documented install script (`curl | INSTALL_DIR=/usr/local/bin bash`), pulling the
    latest GitHub release binary, same unpinned approach as yt-dlp
  - Pre-creates an empty `~/.config/fabric/.env` at build time - fabric refuses to run at all
    without that file existing, even for commands that touch no AI vendor
- **Revealed:**
  - Fabric's `-y/--transcript` flag shells out to yt-dlp itself for YouTube transcript
    extraction (there's even a `--yt-dlp-args` passthrough) - it doesn't replace the yt-dlp
    dependency, it wraps it
  - An empty `.env` is enough to satisfy fabric's startup gate for `-y --transcript`; no
    `fabric --setup` wizard or AI vendor key needed for that specific command. Real vendor
    config only becomes necessary once an AI-using Fabric pattern is actually wired up.
  - Nothing in `Main.Api`/`Infrastructure` calls fabric yet - this stone only makes it
    available in the image, mirroring stone 7's shape. Wiring it into the actual YouTube
    pipeline (replacing or complementing `YouTubeSubtitlesDownloader`) is a separate, bigger
    stone, moved from "deliberately deferred" to "next candidates" now that the tool itself
    is confirmed to work standalone
- **Demo:**
  - `podman build -f Main.Api/Dockerfile -t workflow-api-test .` (docker daemon unavailable in
    the build sandbox, same as stones 3/7)
  - `podman run --rm --entrypoint sh workflow-api-test -c 'fabric --version; tesseract --version; ffmpeg -version; yt-dlp --version'`
    confirms all four tools still present and working
  - `podman run --rm --entrypoint fabric workflow-api-test -y "https://www.youtube.com/watch?v=jNQXAC9IVRw" --transcript`
    returned a real transcript, no API keys configured
- **Test:** infra-only change, no new application logic to pin (matches stones 3/4/7) -
  verified via the podman build + manual demo above
