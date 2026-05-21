# Goal: NET Doc-Only Pending Scenario Warning Cleanup

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Make the known post-MVP `NET-003` and `NET-004` scenario IDs explicit pending entries so the scenario registry gate no longer emits avoidable doc-only warnings while keeping them out of active executable coverage.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a test/catalog hygiene cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or dedicated runners.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, network scenario IDs, or similar identifiers.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` so it fails while `NET-003` and `NET-004` are not explicitly listed as pending scenario IDs.
- Update `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs` through Unity MCP script tooling so the pending comment enumerates `NET-002`, `NET-003`, `NET-004`, `NET-005`, and `TOOL-001` explicitly instead of using a range that the bash gate cannot interpret.
- Do not add active catalog rows for `NET-003` or `NET-004`; they remain post-MVP pending items.
- Do not weaken `Scripts/ci/check-scenario-registry-sync.sh` warning behavior.

## Required TDD

RED:

- Add an EditMode test that asserts `ScenarioId.cs` contains exact pending text for `NET-003` and `NET-004`.
- Confirm the test fails before updating `ScenarioId.cs`.

GREEN:

- Update only the pending scenario comment in `ScenarioId.cs`.
- Confirm the new EditMode test passes.
- Confirm the bash scenario registry gate no longer emits `NET-003` or `NET-004` warnings.

## Required Verification

Run:

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

This cycle does not require PlayMode, screenshots, or visual saved-state checks because it only clarifies pending test scenario metadata.

## Acceptance Criteria

- `NET-003` and `NET-004` are exact pending identifiers in `ScenarioId.cs`.
- `StudentLifeScenarioRegistryCoverageGateTests` protects the explicit pending identifiers.
- The bash scenario registry gate completes without `NET-003` or `NET-004` warnings.
- Scenario EditMode tests and baseline gates pass.
- No runtime gameplay behavior changes are introduced.

## Commit

Use:

```text
[TEST][CHORE] NET pending scenario 경고 정리
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
