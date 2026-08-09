# Adding a new preset

A preset is a full end-to-end workflow assembled from `Infrastructure` tools: construct the tools, chain them via `Workflow`/the `Core/Tools` composition tools (`SequentialTool`, `ParallelTool`, `ConditionalTool`, `FirstSuccessfulTool`), and expose a single async entry method.

Presets are deliberately tightly coupled to the concrete components they use (see `Readme.txt`) — they're convenience wiring for the presentation layer, not a reusable abstraction. Don't introduce interfaces/DI here to decouple a preset from its tools.

Reference implementations: `YouTubeSummary.cs`, `MeetingMinutes.cs`.
