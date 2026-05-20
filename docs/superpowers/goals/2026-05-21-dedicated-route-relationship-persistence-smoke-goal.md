# Goal: Dedicated Route/Relationship Persistence Smoke

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated server/client harness, route/relationship client autoplay runner, and Addressables deletion guard were committed.

## Objective

Add a true dedicated server/client smoke path that can advance route and relationship state in a built client, restart the same save slot, observe the restored state without reapplying gameplay actions, and collect screenshot/log evidence.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Do not use host-mode coverage as a substitute for dedicated server/client coverage.
- Do not run generic `-directValidationAutoplay` with the route/relationship runner.
- Do not add production C# branching on entity, route, quest, activity, location, NPC, crop, tool, furniture, career, job, or similar IDs.
- The first pass may use player-facing input/UI automation. The reload pass must observe restored route/relationship state without applying the route/relationship actions again.
- Capture screenshot evidence from the client flow.
- If Unity tooling removes `Assets/AddressableAssetsData/link.xml` or `.meta`, restore both immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Extend the dedicated route/relationship autoplay installer with an observe-only flag:

```text
-directValidationAutoplayRouteRelationshipObserveOnly
```

- The observe-only path must:
  - wait for the local input player,
  - log route and relationship snapshots,
  - capture screenshot evidence when `-directValidationScreenshot <path>` is supplied,
  - log `autoplay completed route-relationship-observe`,
  - avoid career practice, Daily Plan selection, and Town help action replay.
- Add a QA script:

```text
Scripts/qa/run-dedicated-route-relationship-smoke.ps1
```

- The script must:
  - launch `Builds/Server/Windows/rootborn-server.exe`,
  - launch `Builds/Client/Windows/rootborn.exe` with `-directValidationAutoplayRouteRelationship`,
  - verify first-pass route/relationship completion logs and screenshot,
  - restart server/client with the same save slot,
  - launch the client with `-directValidationAutoplayRouteRelationshipObserveOnly`,
  - verify observe-only route/relationship logs and screenshot,
  - fail on `autoplay failed`, missing screenshot, missing server/client readiness, or fatal log patterns.

## Required TDD

RED:

- Add an EditMode contract test that fails until the installer contains the observe-only flag and completion string.
- Add an EditMode source contract test that fails until the dedicated route/relationship smoke script exists and contains the first-pass and observe-only flags.

GREEN:

- Implement the observe-only runner path.
- Add the PowerShell smoke script.
- Re-run the contract tests and confirm they pass.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If fresh built artifacts can be produced or are already known to include the current commit, also run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
```

If fresh built artifacts cannot be produced in the current session because the Unity project is already open or another environment lock blocks batchmode builds, do not claim full dedicated persistence coverage. Commit only the observe-only runner/script foundation if tests pass, and report the exact blocked command.

## Acceptance Criteria

- The built-client smoke path can launch exactly one action runner on first pass and exactly one observe-only runner on reload pass.
- The route/relationship runner has success, failure, screenshot, and observe-only evidence contracts.
- The QA script verifies true server/client logs and screenshot files instead of relying on host-mode tests.
- Full dedicated persistence coverage is claimed only if the smoke script is run against fresh artifacts that include this commit.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated route relationship persistence smoke 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
