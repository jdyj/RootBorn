# Goal: Relationship Commitment Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Update the Student Life scenario coverage catalog and gate so the already-implemented Teacher relationship commitment PlayMode scenario is tracked as active executable evidence.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a catalog and test-gate cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or dedicated runners.
- Keep relationship behavior ScriptableObject-driven.
- Do not add production C# branching on NPC IDs, relationship IDs, milestone IDs, route IDs, activity IDs, reward IDs, or similar entity identifiers.
- `ObjectiveJournalPanel` owns relationship commitment guidance. `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Work

- Tighten `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` so it fails while `REL-COMMITMENT-001` is absent from active scenario rows.
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`.
- Map `REL-COMMITMENT-001` to existing executable evidence:
  - `Assets/Tests/PlayMode/EndToEnd/RelationshipActivityCommitmentE2EScenarioTests.cs`
  - `Rootborn.Tests.PlayMode.EndToEnd.RelationshipActivityCommitmentE2EScenarioTests.REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload`
  - `Builds/Logs/relationship-activity-commitment-e2e.png`
- Keep `REL-FIRST-GUIDE-001` and `REL-SECOND-NPC-001` unchanged.

## Required TDD

RED:

- Add an EditMode catalog contract that fails while `REL-COMMITMENT-001` is absent from the active rows.

GREEN:

- Catalog contains active `REL-COMMITMENT-001` with concrete PlayMode evidence and screenshot path.
- Scenario registry gates pass.

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

This cycle does not require a PlayMode rerun because it only activates catalog tracking for an already-existing PlayMode scenario. If the evidence file or test method is missing, stop and split into a verification cycle.

## Acceptance Criteria

- `REL-COMMITMENT-001` is active in the scenario catalog.
- The row maps to `RelationshipActivityCommitmentE2EScenarioTests.cs`.
- The row names `REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload`.
- The row references `Builds/Logs/relationship-activity-commitment-e2e.png`.
- Scenario EditMode test and both scenario registry sync gates pass.
- Baseline gates pass.
- No runtime gameplay behavior changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Relationship commitment scenario catalog 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

