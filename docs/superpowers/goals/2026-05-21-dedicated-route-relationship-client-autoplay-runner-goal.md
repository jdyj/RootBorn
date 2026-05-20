# Goal: Dedicated Route Relationship Client Autoplay Runner

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated client autoplay evidence contract.

## Objective

Add a dedicated client-side route/relationship autoplay runner for future dedicated server smoke tests, using a route/relationship-specific command-line flag that does not conflict with the existing generic direct-validation autoplay runner.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a client automation foundation cycle, not the dedicated route/relationship persistence smoke itself.
- Do not run generic `-directValidationAutoplay` together with the route/relationship-specific runner because two input automation loops could conflict.
- The route/relationship runner must use player-facing input/UI actions where implemented.
- The runner must emit explicit success and failure trace lines.
- Screenshot path must come from an explicit command-line argument.
- Do not add production entity-ID branching.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add a dedicated route/relationship autoplay runtime installer keyed by `-directValidationAutoplayRouteRelationship`.
- Add `-directValidationScreenshot <path>` parsing for screenshot evidence.
- Update the harness route/relationship preset so it uses:
  - `-directValidationTrace`,
  - `-directValidationAutoplayRouteRelationship`,
  - `-directValidationScreenshot <path>`.
- Do not include the generic `-directValidationAutoplay` flag in the route/relationship preset.
- The runner should:
  - wait for the local input player,
  - move to career practice and interact,
  - move to Daily Plan board and select a visible route,
  - move to a Town help action and interact,
  - capture screenshot if a path is supplied,
  - log route, relationship, and completion evidence.

## Required TDD

Write failing tests first:

- Harness preset fails if it includes generic `-directValidationAutoplay`.
- Harness preset includes route/relationship flag and screenshot path.
- Source contract test fails until the route/relationship autoplay installer exists.
- Source contract test fails unless the installer logs `autoplay completed route-relationship`.
- Source contract test fails unless the installer logs `autoplay failed`.
- Source contract test fails unless screenshot argument parsing is present.

Expected RED:

- No dedicated route/relationship autoplay runner exists yet.

Expected GREEN:

- The route/relationship autoplay runner compiles, the harness preset avoids the generic runner, and contract tests prove success/failure/screenshot evidence strings are present.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Future dedicated route/relationship smoke can launch exactly one client autoplay runner.
- The route/relationship runner has explicit success, failure, and screenshot evidence contracts.
- The cycle does not claim full dedicated route/relationship persistence coverage.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated route relationship client autoplay 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
