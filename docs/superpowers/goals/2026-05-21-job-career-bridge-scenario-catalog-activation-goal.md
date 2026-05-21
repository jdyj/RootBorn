# Goal: Job Career Bridge Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the current stabilization and feature slices were audited.

## Objective

Update the Student Life scenario catalog and coverage gate so the already-implemented Job/Career bridge slice is tracked as active executable coverage instead of deferred future work.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a catalog and test-gate cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or dedicated runners unless a gate proves the evidence path is invalid.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on job IDs, career IDs, route IDs, work IDs, activity IDs, location IDs, NPC IDs, or similar entity identifiers.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` so it fails while `JOB-CAREER-BRIDGE-001` remains deferred.
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`.
- Map `JOB-CAREER-BRIDGE-001` to existing executable evidence:
  - `Assets/Tests/PlayMode/EndToEnd/CareerJobRouteE2EScenarioTests.cs`
  - `Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload`
- Remove the stale deferred row for `JOB-CAREER-BRIDGE-001`.
- Keep `JOB-PART-TIME-001` as the existing part-time work growth row; the bridge row must specifically document part-time work progress unlocking/selecting the Workshop Daily Plan route and Objective Journal guidance.

## Required TDD

RED:

- Add an EditMode catalog contract test that fails while `JOB-CAREER-BRIDGE-001` is absent from active rows or still appears in deferred rows.
- Confirm the test fails against the current catalog.

GREEN:

- Catalog contains active `JOB-CAREER-BRIDGE-001` with a concrete PlayMode evidence path.
- Catalog no longer has a deferred `JOB-CAREER-BRIDGE-001` row.
- Scenario registry gates pass.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

This cycle does not require PlayMode rerun or screenshot capture because it only updates catalog/gate ownership for an already-existing PlayMode scenario. If the evidence path is found missing or invalid, stop and report that the bridge feature itself needs a separate implementation or verification cycle.

## Acceptance Criteria

- `JOB-CAREER-BRIDGE-001` is active in the scenario catalog.
- The row maps to the existing Career/Job route PlayMode E2E test file.
- The stale deferred row is removed.
- Scenario EditMode tests and both scenario registry sync gates pass.
- Baseline gates pass.
- No runtime gameplay behavior changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Job career bridge scenario catalog 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
