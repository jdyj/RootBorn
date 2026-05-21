# Goal: Dedicated Core Gameplay Matrix Catalog Evidence Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Update the Student Life scenario coverage catalog so the active dedicated core matrix row references the fresh matrix evidence audit and log directory from the latest successful run.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a catalog/test-gate cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or QA runner behavior.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add or use an EditMode catalog contract that fails while the dedicated core matrix catalog row does not reference:
  - `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-evidence-refresh.md`
  - `Builds/Logs/network-dedicated-core-gameplay-matrix-refresh`
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`.
- Keep the existing `NET-CORE-MATRIX-001` and `NET-CORE-MATRIX-RESOURCE-001` rows active.
- Do not alter scenario IDs or add deferred rows.

## Required TDD

RED:

- `StudentLifeScenarioRegistryCoverageGateTests` must fail because the catalog does not reference the latest matrix evidence audit/log path.

GREEN:

- Update the catalog row.
- Re-run the scenario registry tests and sync gates.

## Required Verification

Run:

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- The catalog references the latest dedicated core matrix evidence audit and log directory.
- Scenario registry tests and sync gates pass.
- No runtime gameplay, QA runner, scene, prefab, ScriptableObject, or save format changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Dedicated core gameplay matrix 카탈로그 증거 갱신
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

