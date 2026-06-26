# AtlasForge Project Health Optimization

Date: 2026-06-27

## Goal

Improve project reliability and maintainability without changing the user-facing packing workflow.

## Baseline

- `dotnet build AtlasForge.sln`: passes with 2 analyzer documentation warnings.
- `dotnet test AtlasForge.sln`: passes, 44 tests.
- `dotnet format AtlasForge.sln --verify-no-changes`: passes.
- Working tree already contains unpack/preview/viewmodel changes before this optimization pass.

## Findings

1. Export metadata uses a hard-coded `meta.version = "1.0.0"` while the app project declares `<Version>0.1.1</Version>`.
2. Build emits `EnableGenerateDocumentationFile` warnings because code style analysis wants generated XML documentation enabled for IDE0005.
3. `tests/AtlasForge.Tests/UnitTest1.cs` contains diagnostic tests and a machine-specific path under `D:\Dev\...`; absent files make one test a no-op.
4. `MainViewModel` starts update checking in the constructor, which couples simple ViewModel tests to an external side effect.
5. `MainViewModel.PackAsync()` can be triggered repeatedly by property changes. There is no sequence guard or cancellation, so older pack work may update the UI after newer settings.

## This Pass

### Export metadata version

Acceptance criteria:

- JSON export writes the assembly informational version into `meta.version`.
- A deterministic service test covers the behavior.

### Analyzer warning cleanup

Acceptance criteria:

- App project build no longer emits the `EnableGenerateDocumentationFile` warning.
- `dotnet build AtlasForge.sln` remains green.

### Test hygiene

Acceptance criteria:

- Remove machine-specific/no-op tests.
- Keep deterministic WPF bitmap DPI coverage.
- `dotnet test AtlasForge.sln` remains green.

## Deferred Work

### Update checker injection

Introduce an injectable update-check dependency or startup option for `MainViewModel`.

Acceptance criteria:

- Unit tests can construct `MainViewModel` without network/cache side effects.
- App startup still checks updates by default.

### Pack operation sequencing

Add cancellation or monotonically increasing pack request IDs.

Acceptance criteria:

- Only the latest `PackAsync()` result may update `CurrentAtlas`, `AtlasPreview`, and `StatusMessage`.
- Failed stale pack attempts do not revert `PackingMode` or show stale warnings.

