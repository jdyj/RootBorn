# Feature Development Roadmap - 2026-05-19

## Current Project Read

The project already has broad foundations for Town, Student Life, career growth, relationships, daily events, campaign progression, housing, interiors, quests, world state, discovery clues, SPUM visuals, and multiplayer validation.

The next feature work should not start by adding another isolated system. The strongest next slice is to connect the systems already present into a clearer player-facing loop.

## Current Code/Data Signals

- `Assets/Scripts/Game/StudentLife` is the largest runtime domain with 143 C# files.
- `Assets/Data/StudentLife` is the largest data folder with 152 assets.
- `Assets/Tests/PlayMode/EndToEnd` has the largest PlayMode coverage area with 41 files.
- Recent work stabilized Registry access through `GameDataRegistryProvider`.
- Remaining dirty workspace state is unrelated to feature planning:
  - `D Assets/AddressableAssetsData/link.xml`
  - `D Assets/AddressableAssetsData/link.xml.meta`

## Priority 1: Objective Journal Integrated Tracking Loop

### Recommendation

Build an integrated Objective Journal loop that unifies Student Life, Campaign, Milestone, Quest, Career Hint, and Housing goals under `ObjectiveJournalPanel`.

### Why This Should Be Next

The project already has many progression systems. The current risk is that the player cannot easily understand what to do next or how the systems connect. `ObjectiveJournalPanel` is already the constitutional owner for goal-like UI, so this feature aligns with existing architecture instead of introducing another HUD or panel.

### Feature Shape

- `ObjectiveJournalPanel` shows categorized objective entries.
- `TrackedObjectiveHud` displays only one selected/tracked objective.
- Objective sources provide entries from existing systems:
  - Quest state
  - Campaign day direction
  - Milestone progress
  - Career hints/candidates
  - Student day/tutorial next guide
  - Housing/interior upgrade or placement goals
- Selecting an objective updates the tracked HUD.
- Completing related player actions updates the objective state.
- Save/load preserves tracked objective selection where appropriate.

### Expected Player Value

The player gets a coherent "what should I do now?" loop without losing the open-ended growth design. This makes existing content feel more complete and reduces confusion.

### Suggested Goal Title

`Objective Journal Integrated Objective Tracking and Daily Activity Loop`

## Priority 2: Day Plan / Schedule Selection Loop

### Recommendation

Add a daily planning flow where the player chooses a loose plan for the day, then follows time-slot activities across Town.

### Feature Shape

- Morning plan selection.
- Time-slot activity recommendations.
- At least two alternate growth paths per target:
  - Town activity
  - Relationship/help action
  - Part-time work
  - Self-study
  - Exploration
  - Rest/recovery
- Day result summary compares planned vs actual activities.

### Expected Player Value

This turns Student Life systems into a repeatable daily loop while preserving free-path growth.

## Priority 3: Housing/Interior Growth Rewards

### Recommendation

Connect housing and interior systems to Student Life progression rewards.

### Feature Shape

- Unlock furniture through relationships, careers, jobs, milestones, or world state.
- Room upgrades require currency or activity progress.
- Placed furniture can affect next-day status, energy, focus, or skill growth.
- Interior changes are reflected in Objective Journal when tied to goals.

### Expected Player Value

Housing becomes part of the life-sim progression loop instead of a separate construction feature.

## Priority 4: NPC Relationship and Schedule Deepening

### Recommendation

Make NPCs feel more alive by deepening schedule, relationship, and location-state interactions.

### Feature Shape

- NPC location changes by time slot.
- Relationship stage changes dialogue and help requests.
- Location state influences NPC dialogue/events.
- Journal objectives can point to NPC requests.

### Expected Player Value

Town interaction becomes more authored and reactive without violating the data-driven entity rules.

## Priority 5: Legacy Farm Role Decision

### Recommendation

Decide whether legacy Farm remains isolated, becomes a student-life practice area, or is absorbed into the Town activity/resource loop.

### Options

1. Keep Farm as legacy/migration source only.
2. Reframe Farm as an after-school garden/practice location.
3. Gradually absorb crop/resource mechanics into Student Life activity and location systems.

### Expected Player Value

This removes ambiguity around whether Farm is core gameplay or legacy scaffolding.

## Recommended Next Goal Prompt

```md
# Goal: Objective Journal Integrated Objective Tracking and Daily Activity Loop

## Objective

Unify Quest, Campaign, Milestone, Career Hint, Student Day Guide, and Housing goals through `ObjectiveJournalPanel`. The always-visible HUD must remain limited to `TrackedObjectiveHud`, showing only one tracked objective at a time.

## Core Completion Criteria

- The player can open and close Objective Journal with Tab.
- The Journal displays at least Quest, Campaign, Milestone, and Student Day Guide entries.
- Selecting one objective updates `TrackedObjectiveHud`.
- A real PlayMode flow verifies MainMenu -> Town -> open Journal -> select objective -> perform related activity -> objective state updates.
- A Game View screenshot is captured as evidence.
- New goal/quest/campaign/hint UI does not create separate always-visible Canvas or HUD objects.
- Objective entries are generated from data and progress state without entity ID-specific C# branches.
```

## Verification Expectations For Next Goal

- EditMode provider/source tests for objective entries.
- Static source gate for objective UI ownership.
- PlayMode E2E for Tab journal open, objective selection, tracked HUD update, and one objective progress update.
- Game View screenshot evidence.
- `Scripts/ci/check-no-entity-id-branching.sh`.
- `git diff --check`.
