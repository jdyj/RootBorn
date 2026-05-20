# Autonomous Recurring Feature Improvement Loop Design

## Purpose

Create a reusable prompt that continuously improves ROOTBORN by choosing the next task, writing a focused per-cycle goal, implementing it, verifying it, committing it, and then choosing the next task again.

The loop is intended for long-running autonomous work. It should not stop after writing a goal document. It stops only when the user stops it, a safety blocker appears, or the current cycle cannot be verified.

## Scope

The loop is responsible for:

- auditing current project context,
- identifying three candidate improvements each cycle,
- selecting one candidate without asking the user unless product intent is ambiguous,
- writing a per-cycle goal document under `docs/superpowers/goals/`,
- implementing the selected goal,
- running required tests and static checks,
- performing direct PlayMode visual/player-flow verification where required,
- documenting evidence,
- committing only the completed cycle's files,
- starting the next cycle automatically.

The loop is not responsible for:

- bypassing dirty-workspace safety,
- merging unrelated changes,
- weakening PlayMode or visual verification requirements,
- using direct method calls as the only proof of player-facing behavior,
- continuing after a failed verification gate.

## Operating Model

Each cycle starts with an audit of workspace state, recent commits, recent goals, recent plans, and verification evidence. The loop prioritizes work in this order:

1. Dirty workspace or unknown generated artifacts that could corrupt future work.
2. Broken tests, compilation errors, or broken player-facing flows.
3. Missing visual PlayMode evidence for visible or interactive systems.
4. Foundation stability issues blocking future features.
5. Small user-facing feature slices that advance student life, town, objective journal, multiplayer, placement, or persistence loops.
6. UI quality improvements tied to an existing player workflow.

The loop must select one small cycle and complete it before starting another. Every cycle produces either a commit or a clear stop report.

## Stop Conditions

The loop stops when:

- the user asks it to stop,
- dirty state ownership is unclear and blocks safe work,
- verification fails and cannot be fixed inside the current cycle,
- Unity compilation fails outside the current cycle's scope,
- a required visual/player-flow check cannot be performed,
- the next valuable task needs product judgment that existing docs do not answer,
- the next task is too broad for one reviewable cycle.

## Per-Cycle Goal Requirements

Every cycle writes a goal document that includes:

- Objective
- Non-Negotiable Constraints
- Required Skills
- Required Work
- Required TDD
- Required Verification
- Acceptance Criteria
- Commit guidance
- Loop Continuation

The document is not the endpoint. It is the cycle contract used immediately for implementation.

## Verification Requirements

Every cycle must run `git diff --check`, the no-entity-ID-branching CI script when available, and the narrowest relevant Unity tests.

Visible gameplay, UI, sprite, tilemap, placement, scene, prefab, camera, and interaction changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence. Runtime-only checks are not enough when the result should persist in a scene, prefab, ScriptableObject, palette, tilemap, or serialized asset.

## Safety Rules

The loop must not:

- edit `Assets/**/*.cs` directly through shell commands,
- delete, revert, move, stage, or commit unrelated dirty files,
- use `git reset --hard` or destructive checkout commands,
- add C# entity ID branching,
- create goal-like UI outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`,
- claim a cycle is complete before fresh verification evidence is available.

## Output

The reusable loop prompt is:

- `docs/superpowers/goals/2026-05-20-recurring-next-feature-goal-generator.md`

Despite the historical filename, the file now defines the autonomous recurring improvement loop: select, write goal, implement, verify, commit, repeat.
