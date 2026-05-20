# Goal: Dedicated Route Relationship Long-Run Regression

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated route/relationship smoke passed first-pass and reload observe verification.

## Objective

Add an opt-in repeated dedicated server/client regression that runs the route/relationship flow once, then reloads the same save slot in observe-only mode multiple times to catch process cleanup, persistence drift, duplicate state, and intermittent network/runtime installer ordering failures.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or script changes.
- Do not use host-mode coverage as a substitute for real dedicated server/client process verification.
- Do not add production C# branching on route IDs, activity IDs, relationship IDs, NPC IDs, quest IDs, or similar entity IDs.
- Keep route and relationship behavior data-driven through existing registry, Daily Plan, relationship, and Objective Journal contracts.
- The long-run script must use built executables and player-facing direct validation flags; it must not mutate domain state directly.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables file churn before commit.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:systematic-debugging`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add a repository QA script:

```text
Scripts/qa/run-dedicated-route-relationship-longrun.ps1
```

- The script must accept at least:
  - `ServerExecutable`
  - `ClientExecutable`
  - `RunName`
  - `Cycles`
  - `StartPort`
  - `WaitSeconds`
  - `SaveSlot`
- Cycle 1 must run the action pass using `-directValidationAutoplayRouteRelationship`.
- Cycles 2..N must run observe-only reload passes using `-directValidationAutoplayRouteRelationshipObserveOnly`.
- Each cycle must write isolated server log, client log, and screenshot files under:

```text
Builds/Logs/<RunName>/
```

- The script must assert:
  - server starts,
  - client starts,
  - client connects,
  - Town synchronizes,
  - screenshot exists and is non-zero,
  - no `autoplay failed`,
  - no fatal exception/error patterns,
  - first cycle logs selected Daily Plan route and relationship state,
  - observe cycles restore selected Daily Plan route and relationship state,
  - launched server/client processes are stopped after every cycle.
- Add or extend tests for long-run script contracts and log readiness parsing.
- Keep the existing smoke script as the fast one-pass gate.

## Required TDD

RED:

- Add a test that fails when the long-run script is missing.
- Add a test that fails if the long-run script does not include both action and observe-only flags.
- Add a test that fails if the long-run script does not require one screenshot per cycle.
- Add a test that fails if the long-run script lacks process cleanup validation or cleanup calls.

GREEN:

- The long-run script contract tests pass.
- Existing dedicated smoke harness tests still pass.
- The actual long-run script runs three cycles against built server/client executables.

## Required Verification

Run narrow tests:

```powershell
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
```

Run the real long-run process scenario:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun -Cycles 3 -StartPort 7881 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun -File | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Three dedicated cycles complete with isolated logs and screenshots.
- Cycle 1 applies route/relationship through the dedicated client action runner.
- Cycles 2 and 3 restore selected route and relationship state through observe-only reload without replaying actions.
- No launched server/client process remains alive after script completion.
- No duplicate route, relationship, Objective Journal, HUD, or fatal log pattern appears in the long-run logs.
- Existing one-pass dedicated route/relationship smoke remains available and passing.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated route relationship long-run 회귀 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
