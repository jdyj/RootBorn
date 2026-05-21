# Goal: Dedicated Daily Plan Completion Smoke

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated runner artifact freshness guard cycle.

## Objective

Add a dedicated server/client process smoke where a player selects a Daily Plan route through visible UI, completes it through the player-facing Town flow, sees result feedback, captures screenshots, and verifies the completed route state after observe-only reload.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- The existing untracked recommendation document `docs/superpowers/goals/2026-05-21-recommended-goal-prompts-after-freshness-guard-order.md` is owned by this session and may be committed with this documentation cycle only if it remains part of the selected recommendation evidence.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior, regression, QA script, and build-script changes.
- Do not use host-mode or Editor PlayMode coverage as a substitute for dedicated server/client process evidence.
- Dedicated autoplay must use player-facing movement, input, interactions, and UI button clicks. It must not directly mutate Daily Plan, Objective Journal, HUD, result, or save state.
- Keep route, activity, location, objective, result, and save behavior ScriptableObject-driven.
- Do not add production C# branching on route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, career IDs, job IDs, crop IDs, tool IDs, furniture IDs, or similar entity identifiers.
- Goal-like UI remains owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add a dedicated Daily Plan completion autoplay installer keyed by:

```text
-directValidationAutoplayDailyPlanCompletion
-directValidationAutoplayDailyPlanCompletionObserveOnly
```

- The action pass must:
  - wait for the local input player,
  - enter Town through the normal runtime flow,
  - select a Daily Plan route through visible UI,
  - complete the selected route through player-facing movement, interaction, or the existing location activity/day-end flow,
  - open Objective Journal through the player-facing path,
  - verify no stale completed-route tracked HUD remains,
  - capture a screenshot when `-directValidationScreenshot` is provided,
  - log Daily Plan route, result feedback, Objective Journal, HUD, and save-state snapshots,
  - log `autoplay completed daily-plan-completion`.
- The observe-only reload pass must:
  - load the same save slot,
  - inspect restored Daily Plan completion state,
  - open Objective Journal through the player-facing path,
  - verify no duplicate route row, no duplicate HUD, and no re-applied completion,
  - capture a screenshot when `-directValidationScreenshot` is provided,
  - log Daily Plan route, result feedback, Objective Journal, HUD, and save-state snapshots,
  - log `autoplay completed daily-plan-completion-observe`.
- Add repository runner:

```text
Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1
```

- The runner must:
  - use existing client/server build artifacts,
  - fail before launch if required artifacts are stale or missing,
  - run action and observe-only reload passes with separate logs and screenshots,
  - fail closed on fatal logs, missing completion markers, missing screenshots, missing saved-state evidence, duplicate-state evidence, stale artifacts, or leaked processes.

## Required TDD

RED:

- Add an EditMode contract test that fails while the Daily Plan completion autoplay installer is missing.
- Add a script contract test that fails while `Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1` is missing.
- Tests must require both action and observe-only flags, screenshot support, artifact freshness checks, completion/failure logs, route/result/Journal/HUD snapshots, and no direct domain mutation calls such as `SelectRoute(`, `CompleteRoute(`, `MarkCompleted(`, or direct save-state writes inside the autoplay installer.

GREEN:

- Network contract tests pass.
- Addressables preservation tests pass if `link.xml` preserve entries are changed for the new installer.
- The dedicated runner completes both action and observe-only reload passes.
- Logs and screenshots prove completion, result feedback, Objective Journal cleanup, HUD cleanup, and reload persistence.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
```

Run the real dedicated process smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1 -RunName network-dedicated-daily-plan-completion-e2e -Port 7897 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-daily-plan-completion-e2e -File | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If fresh build artifacts are missing or stale, stop with exact artifact evidence instead of claiming dedicated smoke completion.

## Acceptance Criteria

- Dedicated action pass completes through actual server/client processes and player-facing input/UI.
- Observe-only reload pass logs `autoplay completed daily-plan-completion-observe`.
- Action and reload screenshots exist and are non-zero.
- Logs prove route completion, result feedback, Objective Journal cleanup, HUD cleanup, and restored saved state.
- No duplicate route row, no duplicate HUD, and no re-applied completion are observed after reload.
- Runner cleans up all launched processes.
- Addressables `link.xml` and `.meta` remain preserved.
- No production entity-ID branching or direct domain state mutation is introduced.

## Commit

Use:

```text
[NETWORK][TEST] Daily Plan completion dedicated smoke 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
