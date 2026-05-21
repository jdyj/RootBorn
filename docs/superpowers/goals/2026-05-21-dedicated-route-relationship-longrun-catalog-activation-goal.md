# Goal: Dedicated Route Relationship Long-Run Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Register the freshly verified dedicated route/relationship long-run as active scenario catalog coverage so it remains visible alongside the core dedicated smoke and matrix rows.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a catalog and test-gate cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or QA runners.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, or network scenario IDs.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` so it fails while the long-run row is missing.
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md` with an active `NET` row for the dedicated route/relationship long-run.
- Map the row to:
  - `Scripts/qa/run-dedicated-route-relationship-longrun.ps1`
  - `docs/superpowers/audits/2026-05-21-dedicated-route-relationship-longrun-refresh.md`
  - `Builds/Logs/network-dedicated-route-relationship-longrun-refresh`
- Keep the existing dedicated smoke row separate from the long-run row.

## Required TDD

RED:

- Add an EditMode catalog contract test that fails while the long-run row is absent.
- Confirm the test fails against the current catalog.

GREEN:

- Add the active catalog row.
- Confirm the scenario registry tests and sync gates pass.

## Required Verification

Run:

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

This cycle does not rerun the long-run because `2026-05-21-dedicated-route-relationship-longrun-refresh.md` records the fresh execution evidence from the previous cycle.

## Acceptance Criteria

- Catalog includes active `NET-ROUTE-REL-LONGRUN-001`.
- The row maps to the long-run runner and references the fresh audit/log evidence.
- Scenario registry tests and sync gates pass.
- No runtime gameplay or QA runner behavior changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Dedicated route relationship long-run 카탈로그 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
