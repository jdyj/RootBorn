# Goal: Status Activity Chain

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the Relationship activity commitment route was completed.

## Objective

Add one small, player-facing Status activity chain in Town. A visible Town activity should change a `StatusDefinition`-based student state, Objective Journal should show the next-step hint, and save/load plus repeat interaction must not duplicate the status delta, Journal row, or tracked HUD.

## Current Code Reality

- Existing Status foundations:
  - `Assets/Scripts/Game/StudentLife/StatusDefinition.cs`
  - `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
  - `Assets/Scripts/Game/StudentLife/LocationActivityOutcomes.cs`
  - `Assets/Scripts/Game/StudentLife/LocationActivityProgressDeltaOutcome.cs`
  - `Assets/Data/Status/`
  - `Assets/Data/StudentLife/LocationIdentities/Outcomes/Outcome_Workshop_Status.asset`
- Existing player-facing activity foundations:
  - `LocationActivityDefinition`
  - `LocationActivityRunner`
  - `LocationActivityOutcomeBase`
  - `LocationActivityInteractor`
  - `LocationIdentityPanel`
  - `StudentDayResultPanel`
- Existing coverage to reuse or strengthen:
  - `Assets/Tests/EditMode/StudentLife/LocationIdentityGameplayTests.cs`
  - `Assets/Tests/EditMode/StudentLife/StudentRelationshipConditionTests.cs`
  - `Assets/Tests/EditMode/StatusValueTests.cs`
  - `Assets/Tests/EditMode/UI/Objectives/`
  - `Assets/Tests/PlayMode/EndToEnd/StudentRelationshipConditionDayLoopE2EScenarioTests.cs`
- Current gap:
  - Status changes exist as activity outcomes, but there is no focused Status-only Town activity chain with Objective Journal next-step guidance and exact-once reload/duplicate coverage.
  - Knowledge has core `KnowledgeProgress`, but connecting it to Town activity, persistence, and Objective Journal is a larger cycle. This cycle intentionally chooses Status first.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP `script-update-or-create` for Unity C# files.
- Write failing tests first for behavior changes.
- Keep gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on status IDs, activity IDs, location IDs, route IDs, quest IDs, NPC IDs, crop IDs, tool IDs, furniture IDs, resource IDs, recipe IDs, knowledge IDs, or similar entity IDs.
- Status progression must be expressed through `StatusDefinition` assets and existing or new `LocationActivityOutcomeBase` strategy assets.
- Goal-like guidance must be owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible/interactive work requires actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Avoid performance regressions: do not add `Update()` polling, per-frame full registry scans, runtime string allocation loops, or repeated Instantiate/Destroy loops for guidance refresh.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Select one existing Town location activity or add one new narrow activity that is appropriate for Status progression.
- Add or reuse one `StatusDefinition` asset and one `LocationActivityOutcomeBase` status outcome asset.
- Register any new assets in `Assets/Data/Registry/GameDataRegistry.asset`.
- Add or strengthen EditMode coverage so the status activity:
  - is registered,
  - applies the configured status delta through data references,
  - dedupes by request ID,
  - persists and restores the status value,
  - avoids entity-ID branching in production code.
- Add or strengthen Objective Journal source coverage so the next-step hint uses a stable key and does not collide with route, relationship, career, resource/recipe, or quest guidance.
- Add a player-facing PlayMode E2E scenario that:
  - starts from the SaveSlot UI,
  - enters Town,
  - moves to the activity source with keyboard/player movement,
  - uses the visible interact/UI path,
  - observes the status change and day-result feedback,
  - opens Objective Journal with `Tab`,
  - verifies the next-step hint and exactly one tracked HUD when tracked,
  - captures a screenshot,
  - reloads the same save slot,
  - verifies exact-once status, Journal, and HUD restoration,
  - repeats the activity and verifies duplicate request behavior does not reapply the same request.

## Required TDD

RED:

- Add an EditMode test that fails until the selected status activity and outcome are registered in `GameDataRegistry.asset`.
- Add an EditMode test that fails until the selected activity applies the configured `StatusDefinition` delta through a `LocationActivityOutcomeBase` strategy.
- Add or tighten a duplicate-request test that fails if the same request applies the status delta twice.
- Add an Objective Journal test that fails until status-chain guidance appears with a stable, non-colliding key.
- Add a PlayMode E2E test that fails before the player-facing Town activity path and screenshot evidence exist.

GREEN:

- Registry, activity outcome, duplicate-request, Objective Journal, save/load, and PlayMode tests pass.
- The implementation does not add production entity-ID branching.
- The player-facing E2E produces non-zero screenshot evidence.

## Required Verification

Run narrow EditMode tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status
```

If `Rootborn.Tests.EditMode.Status` is not a valid namespace in the current project, run the concrete status test class instead:

```powershell
tests_run EditMode testClass Rootborn.Tests.EditMode.StatusValueTests
```

Run the player-facing PlayMode scenario:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.KnowledgeStatusActivityChainE2EScenarioTests.KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate
```

Inspect screenshot evidence:

```powershell
Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Required PlayMode Scenario

Create:

```text
KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Move to the selected activity source with player movement.
4. Use visible interaction/UI selection to perform the activity.
5. Verify the selected status value changes.
6. Verify day-result or transient feedback includes the status change.
7. Open Objective Journal with `Tab`.
8. Verify the status-chain next-step hint appears once.
9. Track the hint if applicable and verify exactly one `TrackedObjectiveHud`.
10. Capture screenshot:

```text
Builds/Logs/knowledge-status-activity-chain-e2e.png
```

11. Reload the same save slot through SaveSlot UI.
12. Verify status value, Journal row, and tracked HUD restore without duplication.
13. Repeat the same activity/request path and verify no duplicate request applies the same status delta twice.

## Acceptance Criteria

- One Status activity chain is fully data-driven through ScriptableObject assets and strategy arrays.
- The player can perform the activity through actual Town movement and visible interaction/UI.
- The activity changes a `StatusDefinition` state and records day-result feedback.
- Objective Journal owns the next-step guidance and no new permanent HUD is introduced.
- Save/load restores status, Journal, and HUD state without duplicate rows or duplicate deltas.
- Screenshot evidence exists and has non-zero length.
- Addressables `link.xml` and `.meta` remain preserved.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] Status activity chain 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
