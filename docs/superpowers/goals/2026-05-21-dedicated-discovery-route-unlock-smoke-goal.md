# Goal: Dedicated Discovery Route Unlock Smoke

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated core gameplay matrix cycle.

## Objective

Add a dedicated server/client smoke for the existing Discovery Clue to Daily Plan route unlock feature so the route unlock is verified through real client/server processes, action and observe-only reload passes, screenshots, saved-state evidence, and duplicate-state checks.

## Current Code Reality

- The Discovery route unlock feature already exists in Editor PlayMode:
  - `Assets/Tests/PlayMode/EndToEnd/DiscoveryRouteUnlockE2EScenarioTests.cs`
  - `Assets/Tests/PlayMode/EndToEnd/DiscoveryRouteUnlockSecondSliceE2EScenarioTests.cs`
- Route unlock support already exists:
  - `DailyPlanRouteContext`
  - `DailyPlanRouteContextBuilder`
  - `DailyPlanWorldStateRequirement`
  - `DailyPlanRouteHintObjectiveSource`
- Existing dedicated smoke patterns exist for:
  - route/relationship,
  - career Daily Plan route,
  - Daily Plan completion.
- No dedicated discovery route unlock smoke script or autoplay flags currently exist.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for the dedicated smoke contract.
- This cycle is dedicated QA coverage for an existing feature, not a new Discovery route feature.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on clue IDs, interpretation IDs, world-state IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, crop IDs, tool IDs, furniture IDs, or similar entity identifiers.
- Direct validation autoplay code may use explicit command-line flags, but it must drive the existing player-facing flow as far as the dedicated harness supports and must not directly call route completion, clue completion, interpretation completion, or save-state mutation APIs as its primary proof.
- Goal-like UI remains owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add or extend direct validation autoplay support with explicit flags:

```text
-directValidationAutoplayDiscoveryRouteUnlock
-directValidationAutoplayDiscoveryRouteUnlockObserveOnly
```

- The action pass must:
  - wait for the local input player,
  - enter or validate Town through the normal runtime flow,
  - reveal a Discovery Clue through the visible interaction path used by existing player-flow tests,
  - complete a visible clue interpretation choice,
  - activate the data-driven world-state flag,
  - open Daily Plan and select the unlocked route through visible UI,
  - open Objective Journal through the player-facing input path,
  - verify route guidance and `TrackedObjectiveHud` uniqueness,
  - capture a screenshot when `-directValidationScreenshot` is provided,
  - log clue, interpretation, world-state, Daily Plan route, Objective Journal, HUD, and save-state snapshots,
  - log `autoplay completed discovery-route-unlock`.
- The observe-only reload pass must:
  - load the same save slot,
  - inspect restored clue, interpretation, world-state, selected route, Objective Journal, and HUD state,
  - avoid replaying the discovery or interpretation action,
  - verify no duplicate clue records, interpretation records, route rows, Journal rows, or HUDs,
  - capture a reload screenshot,
  - log `autoplay completed discovery-route-unlock-observe`.
- Add repository runner:

```text
Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1
```

- The runner must:
  - use existing client/server build artifacts,
  - fail before launch if required artifacts are stale or missing,
  - run an action pass and observe-only reload pass with separate logs/screenshots,
  - fail closed on fatal logs, missing completion markers, missing screenshots, stale artifacts, duplicate-state evidence, or leaked processes.

## Required TDD

RED:

- Add an EditMode contract test under `Rootborn.Tests.EditMode.Network` that fails while the discovery route unlock dedicated runner is missing.
- The contract must require:
  - both action and observe-only autoplay flags,
  - screenshot support,
  - artifact freshness checks,
  - server/client readiness checks,
  - action and observe completion markers,
  - clue, interpretation, world-state, route, Journal, HUD, and duplicate-state log assertions,
  - process cleanup assertions,
  - no generic `-directValidationAutoplay` launch.
- Add or tighten source contract coverage that fails if the autoplay installer directly calls route, clue, interpretation, or save-state mutation methods as a shortcut.

GREEN:

- Network EditMode contract tests pass.
- The dedicated runner completes action and observe-only reload passes.
- Logs and screenshots prove unlock, route selection, Objective Journal guidance, HUD uniqueness, reload persistence, and no duplicate state.

## Required Verification

Run narrow contract tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
```

If Unity MCP returns an unusable null response, run the equivalent Unity CLI command and inspect the XML result:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\jdyj\farmer' -runTests -testPlatform editmode -assemblyNames Rootborn.Tests.EditMode -testFilter 'Rootborn.Tests.EditMode.Network' -testResults Builds/Logs/editmode-network-discovery-route-smoke-results.xml -logFile Builds/Logs/editmode-network-discovery-route-smoke.log
Select-String -Path Builds/Logs/editmode-network-discovery-route-smoke-results.xml -Pattern '<test-run'
```

Run the real dedicated smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1 -RunName network-dedicated-discovery-route-unlock-e2e -Port 7907 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-discovery-route-unlock-e2e -File | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If fresh build artifacts are missing or stale, stop with exact artifact evidence instead of claiming smoke completion.

## Acceptance Criteria

- Dedicated action pass unlocks the Discovery route through real server/client processes.
- Observe-only reload proves clue, interpretation, world-state, selected route, Journal, and HUD state persisted.
- Observe-only reload does not replay discovery, interpretation, route selection, or reward-like effects.
- Screenshots for both passes exist and are non-zero.
- Logs prove no duplicate clue records, interpretation records, route rows, Journal rows, HUDs, or fatal client/server errors.
- Runner cleans up all launched server/client processes.
- Addressables `link.xml` and `.meta` remain preserved.
- No production entity-ID branching or direct save-state mutation shortcut is introduced.

## Commit

Use:

```text
[NETWORK][TEST] Discovery route unlock dedicated smoke 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
