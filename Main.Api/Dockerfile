# Build context is the repo root (Main.Api references Core and Infrastructure
# via relative ProjectReference paths).
#
# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Main.Api/Main.Api.csproj Main.Api/
RUN dotnet restore Main.Api/Main.Api.csproj
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY Main.Api/ Main.Api/
RUN dotnet publish Main.Api/Main.Api.csproj -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/storage

# External CLI tools that Infrastructure tools shell out to at runtime:
# - tesseract: TesseractOcrTranscriber (English/French/Japanese language data,
#   matching the languages the tool supports)
# - ffmpeg: YouTubeAudioDownloader (audio extraction)
# - yt-dlp: YouTubeAudioDownloader, YouTubeSubtitlesDownloader
# - fabric (https://github.com/danielmiessler/fabric): not called from any
#   Infrastructure tool yet, installed ahead of wiring it up. Its `-y/--transcript`
#   flag shells out to yt-dlp itself for YouTube transcript extraction. Fabric
#   refuses to run at all without ~/.config/fabric/.env existing, so an empty
#   one is created here - real vendor config only matters once an AI-using
#   Fabric pattern is actually wired up.
# yt-dlp and fabric are both pulled from their latest GitHub release rather
# than pinned: both move fast (YouTube regularly breaks older yt-dlp builds),
# so a stale pinned version is more likely to silently fail than an unpinned
# one. Whisper (WhisperTranscriber) needs no external binary - it's the
# Whisper.net/Whisper.net.Runtime NuGet package, bundled at publish time.
ARG TARGETARCH
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-fra \
        tesseract-ocr-jpn \
        ffmpeg \
        ca-certificates \
        curl \
    && case "$TARGETARCH" in \
        amd64) YTDLP_ASSET=yt-dlp_linux ;; \
        arm64) YTDLP_ASSET=yt-dlp_linux_aarch64 ;; \
        *) echo "unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/${YTDLP_ASSET}" -o /usr/local/bin/yt-dlp \
    && chmod +x /usr/local/bin/yt-dlp \
    && curl -fsSL https://raw.githubusercontent.com/danielmiessler/fabric/main/scripts/installer/install.sh | INSTALL_DIR=/usr/local/bin bash \
    && mkdir -p /root/.config/fabric && touch /root/.config/fabric/.env \
    && apt-get remove -y curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Main.Api.dll"]
