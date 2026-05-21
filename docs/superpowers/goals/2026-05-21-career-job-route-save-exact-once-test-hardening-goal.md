# Goal: Career Job Route Save Exact-Once Test Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Harden the existing Career/Job Daily Plan PlayMode scenario so serialized StudentLife and DailyPlan save files must contain the part-time work and selected workshop route state exactly once before and after reload.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a test-hardening cycle, not a gameplay behavior, data, scene, prefab, UI, or save-format change.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Do not add production C# branching on job IDs, career IDs, route IDs, work IDs, activity IDs, location IDs, NPC IDs, reward IDs, or similar entity identifiers.
- `ObjectiveJournalPanel` owns career/job guidance. `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add a contract test in `Assets/Tests/EditMode/StudentLife/CareerPracticeDailyPlanRouteAssetWiringTests.cs` that fails while `CareerJobRouteE2EScenarioTests.cs` only checks save-file containment.
- Update `Assets/Tests/PlayMode/EndToEnd/CareerJobRouteE2EScenarioTests.cs` so:
  - `student-life-progress.json` contains `work.shift.corner-store` exactly once before reload.
  - the DailyPlan save file contains `daily.workshop.shift` exactly once before reload.
  - both save files still contain those values exactly once after reload.
  - the helper uses an explicit occurrence counter so duplicate serialized state fails visibly.
- Keep the existing player-facing flow unchanged: MainMenu SaveSlot UI, Town entry, keyboard movement, interact key, Daily Plan board, Objective Journal `Tab`, tracked HUD, screenshot.

## Required TDD

RED:

- `CareerPracticeDailyPlanRouteAssetWiringTests` must fail while `CareerJobRouteE2EScenarioTests.cs` lacks exact-once save assertions and an occurrence counter.

GREEN:

- `CareerJobRouteE2EScenarioTests.cs` uses `AssertSaveContainsExactlyOnce`.
- The PlayMode scenario asserts exact-once save state both before and after reload.
- The existing screenshot path remains `Builds/Logs/career-job-route-e2e.png`.

## Required Verification

Run:

```powershell
tests_run EditMode testClass CareerPracticeDailyPlanRouteAssetWiringTests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
Get-ChildItem Builds/Logs/career-job-route-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- The EditMode contract test fails before the PlayMode scenario is hardened and passes afterward.
- The Career/Job PlayMode scenario passes.
- Screenshot evidence exists at `Builds/Logs/career-job-route-e2e.png` and has non-zero length.
- No gameplay code, production data, scene, prefab, UI, or save-format files are changed.
- Baseline gates pass.

## Commit

Use:

```text
[TEST] Career Job route 저장 exact-once 검증 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

