# Filigrane: watermark + anonymize (backend in Workflow)

**Status:** active

## Target

First guess:
- Move filigrane's PDF watermarking backend into Workflow
- Add anonymization starting with metadata-only
- Later: support image files, and expose both features through the filigrane frontend (likely renamed)

## Constraints

- Backend lives in `/home/tigrou/Dev/Workflow` (`Main.Api`)
- Endpoints require `X-Api-Key` (Workflow API key auth)
- Keep filigrane's current async token flow
  - `POST /api/watermark` returns `{ token, downloadUrl, expiresAt, expiresInSeconds }`
  - `GET /api/download/{token}` returns the processed file (single use, expiry)
- Commits per stone, implemented in a git worktree, with a PR opened per stone
- Watermarking (and future anonymization) logic is implemented as `ITool<TIn, TOut>` tools under `Infrastructure/Tools/<Name>/`, following the repo's existing convention (see `Infrastructure/Tools/CLAUDE.md`, e.g. `TextTransformers/AITextTransformer.cs`). API controllers are thin: they build the input stream, call the tool, and translate `Result<T>` into an HTTP response. Per-request options are passed into the tool's constructor rather than as method arguments.
- File-type-specific streams (`PdfStream`, `ImageStream`, `AudioStream`) live in `Core/Models/` as thin `Stream` wrappers, so tools can be typed by content kind rather than by raw `Stream`

## Related efforts

None yet

## Stones laid

1. PDF watermarking backend moved into Workflow as a token-based `ITool` endpoint — `stones/01-pdf-watermarking-moved-into-workflow.md` — `5b9910b`
2. Dev proxy injects X-Api-Key server-side so the browser never sees it — `stones/02-dev-proxy-injects-x-api-key.md` — filigrane PR `#4`
3. Main.Api dockerized and deployed behind Caddy on its own host — `stones/03-dockerize-deploy-main-api-behind-caddy.md` — `6049f44`
4. Main.Api ships standalone on loopback, reverse proxy left to the operator — `stones/04-drop-caddy-ship-main-api-standalone.md` — `ea01ce7`
5. Filigrane's prod nginx wired to Main.Api with server-side X-Api-Key injection — `stones/05-wire-filigrane-prod-nginx-to-main-api.md` — filigrane PR `#6`
6. Main.Api's deploy host port and SSH port made configurable — `stones/06-make-main-api-deploy-ports-configurable.md` — Workflow PR `#10`

## Next candidates

- Add metadata-only anonymization (strip PDF `/Info` dict + XMP) as a new `ITool` under `Infrastructure/Tools/Anonymization/`, mirroring the watermark token flow (`POST /api/anonymize` -> token -> existing `GET /api/download/{token}`) - deepens the Target, unblocked
- Deploy filigrane's frontend+nginx to the VPS (e.g. loopback behind host nginx) - edges toward exposure; this is where the "which humans" gate (edge gating vs. per-user auth) finally has to be decided. Deferred again at stone 6 (user kept filigrane local); still on the table
- Edge-gate a network-reachable filigrane (basic auth / IP allowlist / VPN) as a cheap stand-in for user auth, if a shared-but-restricted deploy is wanted before building real auth

## Deliberately deferred

- Image support
- In-content redaction (manual or AI-assisted)
- Rate limiting parity with filigrane nginx (now wired locally via stone 5's nginx; prod deploy still pending)
- Splitting `Infrastructure` into per-tool-group NuGet packages (single API preferred until a second real consumer with different needs shows up)
- Deploying filigrane's frontend publicly (needs its own user-level auth first, not just the shared service-to-service `X-Api-Key`)
