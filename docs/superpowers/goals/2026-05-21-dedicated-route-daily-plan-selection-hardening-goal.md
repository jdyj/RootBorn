# Goal: Dedicated Route Daily Plan Selection Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after client Addressables build hardening made fresh built clients load registry data correctly.

## Objective

Fix the dedicated route/relationship smoke's current gameplay blocker: the built client reaches Town, performs the career practice board interaction, moves to the Daily Plan board, then fails with `autoplay failed: Daily Plan route button not selectable`.

## Current Evidence

- Client build now succeeds through `BuildClientWindows64`.
- Built client has `Builds/Client/Windows/rootborn_Data/StreamingAssets/aa/settings.json`.
- Dedicated smoke no longer fails with Addressables runtime data or registry load errors.
- Latest smoke logs:
  - `Builds/Logs/network-dedicated-route-relationship-e2e/client-first-player.log`
  - `Builds/Logs/network-dedicated-route-relationship-e2e/server-first-player.log`
- Client log evidence:
  - `[ROOTBORN/ResourceManager] Addressables initialized.`
  - `[ROOTBORN/DataManager] Registry loaded: crops=1, tools=5, resources=2, characterParts=10`
  - `[ROOTBORN/DIRECT-TRACE] autoplay interacted target=CareerPracticeBoard player=client-1`
  - `[ROOTBORN/DIRECT-TRACE] autoplay failed: Daily Plan route button not selectable`

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior changes.
- Do not use host-mode coverage as a substitute for the dedicated server/client smoke.
- Do not add production C# branching on route IDs, activity IDs, career IDs, NPC IDs, quest IDs, or similar entity IDs.
- Keep route availability data-driven through existing Daily Plan, career, relationship, and registry contracts.
- The autoplay runner may inspect visible/runtime UI state for diagnostics, but it must not force route state directly.
- Restore `Assets/AddressableAssetsData/link.xml` and `.meta` if Unity tooling deletes them.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:systematic-debugging`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Trace why the Daily Plan route button is not selectable in the built dedicated client after career practice interaction.
- Add diagnostics or tests that expose whether the route is:
  - absent,
  - visible but disabled,
  - blocked by missing progress,
  - blocked by a missing local player component,
  - blocked by runtime installer timing,
  - or blocked by server/client authority state.
- Fix the root cause at the correct boundary.
- Keep the route/relationship runner player-facing: movement, interaction, board opening, visible route selection, screenshot.
- Ensure the runner captures a screenshot and logs a clear failure summary if future UI selection fails.

## Required TDD

RED:

- Add or tighten an EditMode or PlayMode test that fails for the current dedicated-client Daily Plan route selection condition.
- The failing test must prove the selected route button is unavailable for the same reason observed in the built client, not just assert a source string.

GREEN:

- The route becomes selectable after the career practice interaction through existing data-driven route conditions.
- The dedicated route/relationship runner can select the route without forcing route progress directly.
- Existing Daily Plan, Objective Journal, Network, and Addressables tests remain green.

## Required Verification

Run the narrow relevant tests first:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
```

Then run the build and external smoke:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\Users\jdyj\farmer' -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile 'Builds/Logs/client-build-dedicated-route-daily-plan-selection.log'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Built client no longer fails at `Daily Plan route button not selectable`.
- First-pass dedicated smoke captures screenshot evidence.
- First-pass dedicated smoke logs route and relationship evidence or reaches the next blocker with a more specific root-cause message.
- No production entity-ID branching is introduced.
- Addressables `link.xml` remains present and tracked.

## Commit

Use:

```text
[BUGFIX][NETWORK][TEST] Dedicated route Daily Plan 선택 안정화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
