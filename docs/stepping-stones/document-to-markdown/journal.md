# Document to Markdown: convert uploads to markdown with described assets

**Status:** active

## Target

First guess:
- Expose `POST /api/analysis/markdown`: upload a document, get it back as markdown
- Embedded assets (images, diagrams, charts) are turned into inline content at their original
  position: descriptions first (fully local, no LLM), mermaid later
- Architected so the two asset seams (classification, conversion) can each be swapped for an
  LLM-backed implementation without changing the pipeline shape

Decided in grilling (see `.lavish/document-markdown-*.html` in the main checkout, now archived):
- **Processing = Mode A** for v1: docling CLI does everything locally (no LLM, no Python
  sidecar). `--enrich-picture-description` (+ `--enrich-picture-classes`) produces descriptions;
  no mermaid yet
- **Mermaid** is deferred to a later stone (fill the LLM converter seam), not dropped. Safe
  because output is inline, so swapping a description for mermaid is a local change
- **Local LLM provider** in `ChatClientFactory` is out of scope (Mode A uses no chat client)

## Constraints

- Backend lives in `/home/tigrou/Dev/Workflow` (`Main.Api`); target branch `master`
- Stones land in a dedicated worktree at `../Workflow.Worktrees/document-to-markdown/`, one PR
  per stone; the journal + stone file merge in that same PR (no separate bookkeeping PR)
- `master` has a repo ruleset requiring every change (including doc-only edits) to land via a PR
  that passes a CodeQL check; direct pushes rejected. CodeQL's default setup sometimes doesn't
  fire; an empty retrigger commit (`git commit --allow-empty`) pushed to the PR branch kicks it off
- Tracker: GitHub PRs as the unit (no separate issues), matching the `filigrane` effort
- Capabilities are `ITool<TIn, TOut>` under `Infrastructure/Tools/<Name>/`; external processes go
  through the `IProcessRunner` seam (`Infrastructure/Processes/`). API controllers stay thin: build
  the input stream, call the tool, translate `Result<T>` into an HTTP response. Per-request options
  are constructor args on the tool
- `TreatWarningsAsErrors` is solution-wide: builds fail on warnings
- Endpoints require `X-Api-Key` (except `GET /api/system/status`)
- Existing `DoclingConverter` (path/URL -> md) and `DoclingStreamConverter` (Stream -> md) already
  exist and are tested; they return text-only markdown (images become `<!-- image -->`)

## Related efforts

- `filigrane` (same repo, `Main.Api`): shares the controller/tool conventions and deploy pipeline.
  No code overlap expected with this effort

## Stones laid

1. `POST /api/analysis/markdown` converts a multipart upload to text-only markdown via the existing
   `DoclingStreamConverter` — `stones/01-markdown-endpoint-plain-conversion.md` — PR `#17`

## Next candidates

- **Deepen (Stone 2):** introduce `DocumentMarkdownConverter` + the `IAssetClassifier` /
  `IAssetConverter` seams (docling-backed via `--enrich-picture-description`/`-classes`), producing
  inline descriptions for embedded assets at their original position. Reveals whether the docling
  CLI inlines descriptions in its markdown or whether we must post-process `--image-export-mode
  referenced` output, and whether the two-seam split earns its keep before the LLM impl exists
- **Probe a risk:** run one real enriched conversion (a PDF with a figure) through the docling CLI
  to see exactly what description output looks like in markdown, before committing the Stone 2 shape
- **Deepen (Stone 3):** per-asset degrade semantics (a failed asset keeps its placeholder; doc
  still succeeds) — only meaningful once Stone 2 processes assets

## Deliberately deferred

- Mermaid generation (needs the LLM converter seam) - explicitly a later stone
- URL / non-upload input (reuse `URLDownloader` later)
- Local LLM provider in `ChatClientFactory` (Mode A needs no chat client)
- Richer response envelope with per-asset metadata (add when a caller needs it)
- Tables/formulas: left to docling's native markdown/LaTeX in all modes
