# Goal: Dedicated Client Autoplay Evidence Contract

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated server/client process harness.

## Objective

Extend the dedicated server/client harness contract so future dedicated persistence smoke tests can pass client autoplay/evidence arguments and distinguish successful client player-flow completion from failed or incomplete automation.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is still foundation work. Do not claim dedicated route/relationship persistence coverage in this cycle.
- Keep host-mode, server-only, and true dedicated client-process evidence distinct.
- Client autoplay success must require explicit completion log signals and must fail closed when client logs contain `autoplay failed`.
- Evidence paths must be explicit harness inputs, not hidden defaults.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Extend the dedicated harness config with optional client extra arguments.
- Ensure client `ProcessStartInfo` appends extra args after the base `-mode client -joinIp -port -saveSlot` arguments.
- Add a route/relationship autoplay argument preset for future smoke tests.
- Extend readiness parsing so it can report:
  - server started,
  - client started,
  - client connected,
  - Town synchronized,
  - client autoplay completed,
  - client autoplay failed.
- Ensure `IsReady` remains network readiness only, and add a separate player-flow/evidence readiness signal.
- Add tests proving failed autoplay logs do not pass the player-flow readiness gate.

## Required TDD

Write failing tests first:

- Client start info should include explicit extra autoplay/evidence args.
- Route/relationship autoplay preset should include trace, autoplay, route/relationship mode, and screenshot path args.
- Readiness probe should mark `ClientAutoplayCompleted` when completion log exists.
- Readiness probe should mark `ClientAutoplayFailed` when failure log exists.
- Player-flow evidence readiness should be false when client logs contain failure or lack completion.

Expected RED:

- The harness does not yet expose client extra args or client autoplay evidence readiness.

Expected GREEN:

- The harness can carry future route/relationship client automation args and fail closed on missing/failed client completion logs.

## Required Verification

Run:

```powershell
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Future dedicated smoke tests can pass client autoplay/evidence args through the process harness.
- Readiness distinguishes network connection from player-flow completion.
- Failed or missing client autoplay logs cannot be mistaken for route/relationship smoke success.
- No dedicated route/relationship persistence completion is claimed in this cycle.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated client autoplay evidence 계약 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
