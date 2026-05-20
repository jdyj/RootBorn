# Goal: Town Help Relationship Outcome Preservation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add focused regression coverage and linker preservation for the existing data-driven `TownHelpAction` relationship outcome. This is a foundation slice for the later NPC relationship milestone feature.

## Non-Negotiable Constraints

- Keep the slice small: add relationship outcome tests and linker preservation only.
- Do not add a new NPC milestone UI, permanent HUD, scene object, prefab, or player-facing panel in this cycle.
- All behavior must remain ScriptableObject-driven.
- Do not branch production C# on specific help action, NPC, relationship, route, location, quest, crop, tool, or activity IDs.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests and production scripts.
- Follow TDD: write the failing tests first, then implement the minimal production code.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add focused coverage proving `HelpActionRelationshipDeltaOutcome` updates `StudentLifeProgress` relationship state through existing generic relationship APIs.
- Verify the outcome returns the existing `TownHelpActionLogCodec` delta log format so day result/save dedupe behavior remains consistent.
- Preserve the new outcome type in `Assets/AddressableAssetsData/link.xml`.
- Extend the `link.xml` preservation guard so future stripping regressions catch this new data-driven outcome type.

## Required TDD

RED:

- Add an EditMode test proving a `TownHelpAction` can apply a relationship delta outcome.
- Add the new outcome type to the `link.xml` preservation guard.
- Confirm the preservation guard fails before implementation because the relationship outcome type is not preserved.

GREEN:

- Update `link.xml`.
- Re-run the focused EditMode tests and confirm they pass.

## Required Verification

Run:

```powershell
tests_run EditMode testClass Rootborn.Tests.EditMode.StudentLife.TownHelpActionGrowthTests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

No PlayMode or screenshot verification is required in this cycle because it adds a non-visible reusable ScriptableObject outcome primitive and does not change visible gameplay, UI, scene, prefab, camera, tilemap, placement, or interaction flows.

## Acceptance Criteria

- `TownHelpActionRunner` can apply relationship deltas through the existing reusable SO outcome.
- Duplicate request protection continues to prevent double application.
- The new outcome is covered by focused EditMode tests.
- The new outcome is preserved in `link.xml` and covered by the preservation guard.
- No production entity ID branching is introduced.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[FEATURE][TEST] Town Help 관계 outcome 기반 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
