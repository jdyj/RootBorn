# Goal: Addressables `link.xml` Preservation Guard

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add a focused EditMode guard that prevents critical registry-driven Discovery route unlock types from silently dropping out of `Assets/AddressableAssetsData/link.xml`.

## Non-Negotiable Constraints

- This is a foundation/test cycle, not gameplay behavior work.
- Do not change player-facing UI, scenes, prefabs, or gameplay behavior.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests.
- Keep all gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, clue, route, location, activity, quest, world-state, crop, tool, or NPC IDs.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add `Assets/Tests/EditMode/Addressables/AddressablesLinkXmlPreservationTests.cs`.
- Parse `Assets/AddressableAssetsData/link.xml` as XML.
- Assert that current registry, Discovery Clue, Daily Plan route, World State, and Student Life location/activity types needed by route unlocks are preserved with `preserve="all"`.
- If a current required type is missing, update only `Assets/AddressableAssetsData/link.xml`.

## Required TDD

RED:

- Add the EditMode preservation test first.
- Run it and confirm it fails on a missing current route unlock support type.

GREEN:

- Add the missing focused `link.xml` entries.
- Re-run the preservation test and confirm it passes.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

No PlayMode or visual screenshot verification is required because this cycle only changes a linker preservation file and an EditMode test. It does not alter visible gameplay, UI, scene, prefab, camera, tilemap, placement, or interaction behavior.

## Acceptance Criteria

- `link.xml` exists and remains valid XML.
- The new test fails with an actionable missing-type message if a critical route unlock support type is absent.
- The guard covers current registry, route unlock, clue interpretation, Daily Plan route, world-state, and location/activity data types needed by recent flows.
- No gameplay behavior or UI behavior changes are included.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[TEST] Addressables link.xml 보존 가드 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
