# Objective Journal Integrated Tracking Design - 2026-05-19

## Purpose

ROOTBORN already has many progression systems: Quest, Campaign, Milestone, Student Day Guide, Career Hint, Housing, World State, and exploration loops. The next feature should make those systems understandable from the player perspective by turning them into one coherent "what should I do now?" surface.

This design extends the existing `ObjectiveJournalPanel` and `TrackedObjectiveHud` instead of creating another goal UI. It keeps the constitutional rule that `Tab` opens the Objective Journal and the always-visible HUD shows only one tracked objective.

## Existing Baseline

Existing implementation already includes:

- `Assets/Scripts/UI/Objectives/ObjectiveJournalModels.cs`
  - `ObjectiveJournalCategory`
  - `ObjectiveJournalItem`
  - `ObjectiveJournalDetail`
- `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`
  - category rail/list/detail layout
  - `SetItems(categoryId, items)`
  - `SetVerticalSliceSummary(summary)`
  - `TrackSelectedItem()`
- `Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs`
  - compact one-objective HUD
- `Assets/Tests/PlayMode/Objectives/ObjectiveJournalInputPlayModeTests.cs`
  - verifies actual Tab input opens Objective Journal through `ModernUiPanelInputRouter`
- Rule doc: `.claude/rules/objective-journal-ui.md`

The gap is not the panel shell. The gap is a real provider layer that gathers live objective rows from multiple systems and refreshes them after player actions.

## Design Goals

1. Show player-facing objectives from existing systems in one Journal.
2. Keep `TrackedObjectiveHud` as the only always-visible goal HUD.
3. Use data and progress state. Do not add entity ID-specific C# branching.
4. Allow each domain to own its conversion from domain state to `ObjectiveJournalItem`.
5. Refresh explicitly after known state changes, not through continuous `Update()` polling.
6. Keep the first implementation small enough for a single vertical slice and test it through actual PlayMode input.

## Non-Goals

- Do not redesign the visual shell of `ObjectiveJournalPanel` in this slice.
- Do not introduce a separate quest/campaign/milestone always-on HUD.
- Do not implement every possible objective source at once.
- Do not add localization infrastructure beyond existing display key fallbacks.
- Do not add ID-specific special cases like `if (questId == "...")`.

## Proposed Architecture

### Objective Journal Source Contract

Add a small source/provider boundary:

```csharp
public interface IObjectiveJournalSource
{
    string SourceId { get; }
    ObjectiveJournalCategory Category { get; }
    ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context);
}
```

`ObjectiveJournalContext` should carry only current state references:

- `GameDataRegistry Registry`
- `GameObject Player`
- `SaveSlotMetadata ActiveSave`
- optional progress components discovered from the player or scene

Providers must be deterministic readers. They should not mutate game state.

### Aggregator

Add an `ObjectiveJournalProvider` or `ObjectiveJournalAggregator` that:

1. Collects registered `IObjectiveJournalSource` instances.
2. Builds items for all categories.
3. Sorts by `SortPriority`, then title/stable key.
4. Calls `ObjectiveJournalPanel.SetItems(categoryId, items)`.
5. Restores tracked objective selection when possible.

The aggregator is the only class that coordinates multiple domains. Each source stays narrow.

### Source Set for First Slice

The first implementation should include these sources:

1. **Quest Source**
   - Reads `QuestLogPanel.QuestLog` or runtime quest state.
   - Produces active/completed/reward-available quest rows.
   - Progress text comes from objective progress.

2. **Campaign Source**
   - Reads active campaign day/progress.
   - Produces the current route/day direction row.
   - Next hint uses campaign next guide text.

3. **Milestone Source**
   - Reads `MilestoneProgressComponent`/milestone progress.
   - Produces rows for incomplete milestones and recently completed milestones.
   - Progress text summarizes objective completion.

4. **Student Day Guide Source**
   - Reads `StudentLifeProgressComponent`.
   - Produces the current tutorial/next guide row.
   - This is the minimum "what should I do today?" fallback.

5. **Housing Source**
   - Reads existing house state/upgrade availability if present.
   - Produces a row only when there is a meaningful next housing action.
   - If housing state is unavailable, source returns an empty array.

Career hints can be added either in this slice or immediately after. If added now, it should be a sixth source using existing Career Hint/Candidate progress, not special-case ID branches.

## Data Flow

1. Town scene bootstraps normal runtime objects.
2. `ModernUiPanelAutoInstaller` ensures `ObjectiveJournalPanel` and `TrackedObjectiveHud`.
3. Objective Journal aggregator finds or receives:
   - active `GameDataRegistry` through `GameDataRegistryProvider`
   - player progress components
   - existing UI/runtime state such as `QuestLogPanel`
4. Aggregator builds category item arrays.
5. Panel receives item arrays through `SetItems`.
6. Player opens Journal with Tab.
7. Player selects one objective and chooses Track.
8. `TrackedObjectiveHud` displays that one objective.
9. After a relevant activity, quest, campaign, milestone, or day guide state changes, an explicit refresh updates the panel and HUD.

## Refresh Policy

Use explicit refresh hooks, not frame polling.

Initial slice refresh events:

- Town scene loaded and runtime installers completed.
- Objective Journal opened.
- Quest log state changed.
- Student life activity result applied.
- Campaign progress applied.
- Milestone progress applied.
- House upgrade or placement state changed.

If an existing domain has no event, the first implementation can refresh on Journal open and after known E2E actions. Avoid `Update()` scans.

## Tracking and Persistence

The tracked objective is identified by `ObjectiveJournalItem.StableKey`.

Persist:

- save slot id
- tracked stable key
- optional category id

Restore behavior:

1. If the same stable key exists after reload, track it.
2. If it no longer exists but the category still has items, track the first highest-priority item only if it is a guidance fallback.
3. If no useful item exists, hide `TrackedObjectiveHud`.

Persistence can start with a small JSON save file under the active save slot. It must not mutate domain progress.

## UI Behavior

- `Tab` opens/closes Objective Journal through `ModernUiPanelInputRouter`.
- Journal owns full list/detail display.
- `TrackedObjectiveHud` shows exactly one tracked objective.
- No source creates its own persistent Canvas or always-visible goal panel.
- Existing temporary result panels/toasts may remain for immediate action feedback, but they must not become objective lists.

## Performance Notes

- Objective rows are model data first, UI rows second.
- Build source results on explicit refresh.
- Keep source output arrays small in the first slice.
- If lists grow, add paging or row virtualization before rendering hundreds of entries.
- Avoid repeated scene-wide scans during gameplay. If a source needs a scene object, resolve it during refresh and cache weakly where safe.

## Testing Strategy

### EditMode

- Provider aggregation returns categories and sorted items from multiple fake sources.
- Quest/Campaign/Milestone/Student Day source tests build correct `ObjectiveJournalItem` values from controlled progress.
- Tracking persistence restores stable key when item still exists.
- Static source gate verifies new objective/goal/campaign/hint UI does not introduce separate persistent HUD/Canvas ownership.

### PlayMode

Main E2E:

1. Start from MainMenu/New Game flow.
2. Enter Town.
3. Open Objective Journal with actual Tab input.
4. Verify Journal shows at least Quest, Campaign, Milestone, and Student Day Guide rows.
5. Select an objective through UI/player-facing flow.
6. Track it and verify `TrackedObjectiveHud` updates.
7. Perform one related activity through actual player input or interaction.
8. Verify the objective state/progress text updates.
9. Capture Game View screenshot evidence.

### Required Gates

- `Scripts/ci/check-no-entity-id-branching.sh`
- `git diff --check`
- Relevant EditMode namespace/method tests
- Relevant PlayMode E2E method

## Proposed Implementation Order

1. Add failing EditMode tests for aggregator/source model behavior.
2. Add aggregator and source contract.
3. Add first source implementations: Student Day Guide, Quest, Campaign, Milestone.
4. Wire aggregator into existing Objective Journal installer/runtime path.
5. Add tracking persistence and HUD refresh.
6. Add Housing source if stable state access is straightforward; otherwise document as immediate follow-up.
7. Add PlayMode E2E and screenshot evidence.
8. Add static UI ownership gate.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Objective Journal becomes another large polling manager | Refresh on explicit events and Journal open only. |
| Domain-specific ID branches creep into sources | Build rows from definitions/progress arrays; enforce entity branching gate. |
| HUD clutter returns through new panels | Add static source gate for goal-like UI ownership. |
| Quest/Campaign/Milestone progress APIs differ too much | Keep each source as a narrow adapter and aggregate only the common `ObjectiveJournalItem` model. |
| Large lists hurt UI performance later | Keep model/view separation and add paging before scaling. |

## Recommended Next Goal

```md
# Goal: Objective Journal Integrated Objective Tracking and Daily Activity Loop

## Objective

Unify Quest, Campaign, Milestone, Student Day Guide, and Housing goals through `ObjectiveJournalPanel`. The always-visible HUD must remain limited to `TrackedObjectiveHud`, showing only one tracked objective at a time.

## Completion Criteria

- The player can open and close Objective Journal with Tab.
- Journal displays at least Quest, Campaign, Milestone, and Student Day Guide entries.
- Selecting one objective updates `TrackedObjectiveHud`.
- One tracked objective persists across save/load when the stable key still exists.
- A real PlayMode flow verifies MainMenu -> Town -> open Journal -> select objective -> perform related activity -> objective state updates.
- A Game View screenshot is captured as evidence.
- New goal/quest/campaign/hint UI does not create separate always-visible Canvas or HUD objects.
- Objective entries are generated from data and progress state without entity ID-specific C# branches.
```
