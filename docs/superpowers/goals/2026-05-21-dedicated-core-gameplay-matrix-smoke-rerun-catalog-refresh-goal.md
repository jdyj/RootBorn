# Goal: Dedicated Core Gameplay Matrix Smoke Rerun Catalog Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Update the Student Life scenario coverage catalog so `NET-CORE-MATRIX-001` points to the latest dedicated core gameplay matrix smoke rerun evidence captured after freshness source normalization.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to catalog coverage tests and documentation.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Work

- Update `StudentLifeScenarioRegistryCoverageGateTests.cs` so the `NET-CORE-MATRIX-001` contract expects the latest smoke rerun audit and log directory.
- Verify RED against the old catalog row.
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`.
- Verify GREEN and scenario registry gates.

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

- `SCENARIO_REGISTRY_EDIT_010_CatalogReferencesDedicatedCoreMatrixRefreshEvidence` fails before the catalog row update and passes after it.
- The catalog references `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-smoke-rerun-after-freshness-normalization.md`.
- The catalog references `Builds/Logs/network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun`.
- Scenario registry sync gates pass.

## Commit

Use:

```text
[TEST][DOCS] Dedicated core gameplay matrix smoke 카탈로그 증거 갱신
```

## Loop Continuation

After this goal is implemented, verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

