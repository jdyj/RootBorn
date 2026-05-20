# Goal: Town Help First Guide Relationship Wiring

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Wire the existing `HelpActionRelationshipDeltaOutcome` into the First Guide town help action so the player-facing help action advances relationship progress as well as generic growth.

## Non-Negotiable Constraints

- This is an asset/data wiring slice over existing behavior primitives.
- Do not create a new permanent HUD, Canvas, scene object type, or feature-specific UI panel.
- All behavior must remain ScriptableObject-driven.
- Do not branch production C# on specific help action, NPC, relationship, route, location, quest, crop, tool, or activity IDs.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests.
- Create or modify Unity assets through Unity editor/MCP APIs, preserving `.meta` files.
- Visible or interactive impact must be verified through the actual player-facing PlayMode help action flow and Game View/state evidence.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Add or wire one `HelpActionRelationshipDeltaOutcome` asset under `Assets/Data/StudentLife/TownHelpActions/Outcomes/`.
- The outcome must reference `Assets/Data/Relationships/Relationship_FirstGuide_Trust.asset`.
- Append it to `Assets/Data/StudentLife/TownHelpActions/TownHelp_FirstGuideErrand.asset` without removing the existing growth outcome.
- Add EditMode asset wiring coverage.
- Tighten the existing Town Help PlayMode E2E so actual keyboard movement and interact input prove relationship progress, result UI text, save, reload, and duplicate protection.

## Required TDD

RED:

- Add EditMode asset wiring assertion first and confirm it fails because the First Guide help action has no relationship outcome.
- Add PlayMode assertions for relationship state before wiring the asset and confirm the player-facing flow fails at the missing relationship outcome/state.

GREEN:

- Create and wire the relationship outcome asset.
- Re-run EditMode and PlayMode tests and confirm they pass.

## Required Verification

Run:

```powershell
tests_run EditMode testClass Rootborn.Tests.EditMode.StudentLife.TownHelpActionGrowthTests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.TownHelpActionGrowthE2EScenarioTests.HELP_E2E_001_006_PlayTownHelpActionResultReloadAndDedupe
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

The PlayMode verification must use the existing real player-facing route:

```text
MainMenu SaveSlot -> Town -> keyboard movement to TownHelpActionPoint -> interact key -> day-end board -> result UI -> reload same SaveSlot -> result UI again
```

## Acceptance Criteria

- First Guide help action keeps the existing growth outcome and gains one relationship outcome.
- The relationship outcome references the registered First Guide trust relationship definition.
- Actual PlayMode help action input increases relationship progress.
- Day result UI and save/reload state reflect the relationship delta without duplicate application.
- No production entity ID branching is introduced.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[ASSET][TEST] Town Help First Guide 관계 outcome 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
