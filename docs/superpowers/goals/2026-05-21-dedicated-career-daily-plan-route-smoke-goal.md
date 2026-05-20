# Goal: Dedicated Career Daily Plan Route Smoke

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the House upgrade/interior QA runner cycle.

## Objective

Add the next smallest dedicated server/client gameplay smoke: a career practice unlocks a career-gated Daily Plan route, the route is selected through player-facing UI, screenshots/logs prove completion, and an observe-only reload pass verifies career hint and selected route state persisted.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior or script changes.
- Do not use host-mode or Editor PlayMode coverage as a substitute for dedicated server/client process evidence.
- Keep career, route, activity, and objective behavior data-driven through existing ScriptableObject definitions and strategy arrays.
- Do not add production C# branching on route IDs, career IDs, practice IDs, NPC IDs, quest IDs, activity IDs, or similar entity identifiers.
- Dedicated autoplay must use player-facing movement, interact input, and UI button clicks. It must not mutate StudentLife or DailyPlan domain state directly.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add a dedicated career Daily Plan autoplay installer keyed by:

```text
-directValidationAutoplayCareerDailyPlanRoute
-directValidationAutoplayCareerDailyPlanRouteObserveOnly
```

- The action pass must:
  - wait for the local input player,
  - move to `CareerPracticeBoard`,
  - interact through input enough to unlock the existing interior career hint,
  - move to `DailyPlanBoard`,
  - select the newly unlocked career Daily Plan route through visible UI,
  - capture screenshot evidence when `-directValidationScreenshot` is provided,
  - log career and Daily Plan snapshots,
  - log `autoplay completed career-daily-plan-route`.
- The observe-only pass must:
  - wait for local player and DailyPlan/StudentLife runtime,
  - capture screenshot evidence,
  - log career and Daily Plan snapshots,
  - log `autoplay completed career-daily-plan-route-observe`.
- Add repository runner:

```text
Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1
```

- The runner must run first action pass and second observe-only reload pass using built dedicated server/client artifacts, with separate logs and screenshots.
- The runner must fail closed on fatal log patterns, missing completion markers, missing career hint evidence, missing selected route evidence, missing screenshots, or leaked processes.

## Required TDD

RED:

- Add or tighten an EditMode contract test that fails while the dedicated career Daily Plan autoplay installer is missing.
- Add or tighten a script contract test that fails while `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1` is missing.
- Tests must require both action and observe-only flags, screenshot support, completion/failure logs, career snapshot logs, Daily Plan snapshot logs, and no direct domain mutation calls such as `UnlockCareerHint(` or `SelectRoute(` inside the autoplay installer.

GREEN:

- Contract tests pass.
- The dedicated runner can launch server/client action and observe-only reload passes.
- Logs and screenshots prove the career hint and selected route persisted.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
```

Run the real dedicated process smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1 -RunName network-dedicated-career-daily-plan-route-e2e -Port 7893 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-career-daily-plan-route-e2e -File | Select-Object Name,Length,LastWriteTime
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

- Dedicated career Daily Plan action pass completes through player-facing input/UI and logs `autoplay completed career-daily-plan-route`.
- Observe-only reload pass logs `autoplay completed career-daily-plan-route-observe`.
- First and reload screenshots exist and are non-zero.
- Logs prove career hint state and selected Daily Plan route are non-empty after action and after reload.
- Runner cleans up all launched processes.
- No production entity-ID branching or direct domain state mutation is introduced.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated career Daily Plan route smoke 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
