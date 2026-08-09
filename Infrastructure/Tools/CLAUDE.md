# Adding a new tool

A "tool" here is an `ITool<TIn, TOut>` implementation (see root `CLAUDE.md` for the interface). Adding one to `Downloaders/`, `Recorders/`, `TextTransformers/`, `Transcribers/`, or a new subfolder:

1. Write the failing test first in `Tests/Infrastructure/`, mirroring an existing one such as `AITextTransformerTests.cs`.
2. Implement `Transform` returning `Result<TOut>` (FluentResults) — no throw-based control flow to callers. Wrap external/library exceptions in `Result.Fail(new Error(...).CausedBy(ex))` rather than letting them propagate.
3. Don't add bespoke branching/looping/retry orchestration inside the tool. If a capability needs to run other tools in sequence, in parallel, conditionally, or as a fallback chain, compose it from `SequentialTool`, `ParallelTool`, `ConditionalTool`, or `FirstSuccessfulTool` (`Core/Tools/`) instead.

Reference implementation: `TextTransformers/AITextTransformer.cs` + `Tests/Infrastructure/AITextTransformerTests.cs`.
