# Recurring Next Feature Goal Generator Design

## Purpose

Create a reusable prompt that repeatedly selects the next valuable ROOTBORN improvement and writes one executable goal document for a separate development session.

The loop is intentionally split into two sessions:

1. A planning session chooses and writes the next goal.
2. A development session executes that goal with TDD, verification, and commits.

This keeps each improvement reviewable, prevents large mixed-scope sessions, and preserves the repository's strict PlayMode and visible verification gates.

## Scope

The generator prompt is responsible for:

- auditing current project context,
- identifying three candidate improvements,
- recommending one candidate,
- writing one goal document under `docs/superpowers/goals/`,
- verifying the goal document is internally consistent,
- leaving implementation for a later goal session.

The generator prompt is not responsible for:

- writing production code,
- editing Unity assets,
- running full PlayMode verification,
- starting broad refactors,
- committing unrelated dirty files.

## Operating Model

Each loop starts from the current repository state. The generator reads recent goals, plans, audits, specs, recent commits, and workspace status. It prioritizes work in this order:

1. Dirty workspace or unknown generated artifacts that could corrupt future work.
2. Broken or unverified player-facing flows.
3. Missing visual PlayMode evidence for visible or interactive systems.
4. Foundation stability issues blocking future features.
5. Small user-facing feature slices that advance the student life, town, objective journal, multiplayer, placement, or persistence loops.

The generator must select exactly one recommended next goal unless the workspace is unsafe. If the workspace is unsafe, the generated goal must be a triage or stabilization goal.

## Goal Document Requirements

Every generated goal document must include:

- Objective
- Non-Negotiable Constraints
- Required Skills
- Required Work
- Required TDD
- Required Verification
- Acceptance Criteria
- Commit guidance
- Loop Continuation

Visible gameplay, UI, sprite, tilemap, placement, scene, prefab, camera, and interaction changes must require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence. Asset or scene persistence must be verified separately in EditMode when the result is expected to survive reload.

## Safety Rules

The generator must not:

- edit `Assets/**/*.cs` directly through shell commands,
- delete, revert, move, or stage unrelated dirty files,
- use `git reset --hard` or destructive checkout commands,
- propose C# entity ID branching,
- create goal-like UI outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`,
- claim implementation completion, because implementation is out of scope for the generator.

## Output

The main reusable artifact is:

- `docs/superpowers/goals/2026-05-20-recurring-next-feature-goal-generator.md`

That file can be pasted into a new goal session whenever the user wants the next improvement prompt generated.
