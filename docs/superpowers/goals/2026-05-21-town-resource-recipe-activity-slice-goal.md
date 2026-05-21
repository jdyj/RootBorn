# Goal: First Resource/Recipe Town Activity Slice

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Objective Journal source collision regression hardening.

## Objective

Add the first small Town resource/recipe activity slice: a player-facing Town activity grants or reveals a resource/recipe progression state, Objective Journal shows the next-step hint, and save/load plus repeat interaction cannot duplicate the progression.

## Current Code Reality

- Existing resource/crafting foundations:
  - `Assets/Scripts/Game/Resources/ResourceNodeDefinition.cs`
  - `Assets/Data/Resources/Resource_Tree.asset`
  - `Assets/Data/Resources/Resource_Rock.asset`
  - `Assets/Scripts/Game/Crafting/RecipeDefinition.cs`
  - `Assets/Scripts/Game/Crafting/GatherResourceStep.cs`
  - `Assets/Tests/EditMode/Crafting/CraftingRecipeTests.cs`
- Existing Town activity foundations:
  - `LocationActivityDefinition`
  - `LocationActivityRunner`
  - `LocationActivityOutcomeBase`
  - `LocationActivityInteractor`
  - `LocationIdentityPanel`
- Current gap:
  - `GameDataRegistry` exposes resources but not recipes.
  - There is no resource/recipe activity progress model.
  - There is no Objective Journal source for resource/recipe next-step guidance.
  - There is no player-facing Town E2E for resource/recipe activity progression.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production branching on resource IDs, recipe IDs, activity IDs, location IDs, route IDs, quest IDs, NPC IDs, crop IDs, tool IDs, or similar entity IDs.
- Resource/recipe progression must be data-driven through ScriptableObject references and strategy arrays.
- Reward/progression application must be atomic and idempotent for the same request.
- Objective Journal owns next-step guidance. Do not add a new always-visible resource/recipe HUD.
- Visible/interactive work requires player-facing PlayMode flow, Game View screenshot evidence, runtime state inspection, and reload verification.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add recipe registration support to `GameDataRegistry`.
- Add a small resource/recipe progress model and persistence path scoped by save slot and player ID.
- Add a `LocationActivityOutcomeBase` implementation that reveals or progresses a referenced resource/recipe definition without entity-ID branching.
- Wire one Town location activity to the resource/recipe outcome through ScriptableObject data.
- Add one recipe asset under:

```text
Assets/Data/Recipes/
```

- Register the recipe and any new outcome/activity assets in:

```text
Assets/Data/Registry/GameDataRegistry.asset
```

- Add an Objective Journal source for resource/recipe next-step guidance.
- Add player-facing PlayMode E2E coverage:
  - enter Town from SaveSlot flow,
  - use visible Town activity/location UI,
  - verify resource/recipe progress,
  - open Objective Journal with Tab,
  - verify the next-step hint,
  - capture screenshot,
  - reload same slot,
  - verify progress and Journal hint restore exactly once,
  - repeat interaction and verify no duplicate reward/progression.

## Required TDD

RED:

- Add EditMode test that fails before `GameDataRegistry` exposes recipes.
- Add EditMode test that fails before resource/recipe progress persists by save slot and player ID.
- Add EditMode test that fails before a location activity outcome can reveal/progress a referenced recipe idempotently.
- Add EditMode test that fails before Objective Journal resource/recipe source exists.
- Add PlayMode E2E or contract that fails before the player-facing Town activity path and screenshot evidence exist.

GREEN:

- Registry, progress, idempotency, Objective Journal, and PlayMode tests pass.
- The player-facing E2E produces screenshot evidence:

```text
Builds/Logs/town-resource-recipe-activity-e2e.png
```

## Required Verification

Run narrow EditMode tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Crafting
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

Run the player-facing PlayMode scenario:

```powershell
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.TownResourceRecipeActivityE2EScenarioTests
```

Inspect screenshot evidence:

```powershell
Get-ChildItem Builds/Logs/town-resource-recipe-activity-e2e*.png | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Recipe data is registered through `GameDataRegistry`.
- Resource/recipe progress is persisted by save slot and player ID.
- A Town activity can reveal/progress a recipe through SO references, not production ID branching.
- Repeating the same request does not duplicate progression.
- Objective Journal shows resource/recipe next-step guidance and remains the only guidance owner.
- PlayMode E2E verifies the visible player-facing flow, screenshot evidence, reload, and duplicate prevention.
- Addressables `link.xml` and `.meta` remain preserved.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] Town resource recipe activity slice
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
