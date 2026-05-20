# Goal: Autonomous Recurring Feature Improvement Loop

Paste this prompt into a long-running goal session when you want the agent to continuously improve ROOTBORN by selecting the next best task, writing a focused goal for that task, executing it, testing it, committing it, and then selecting the next task again.

This is not a goal-only generator. It is an autonomous improvement loop.

## Objective

Continuously run this loop until the user stops it, a blocker requires user judgment, or the session cannot verify the next completed change:

```text
Audit current state
-> choose next highest-value improvement
-> write a focused per-cycle goal document
-> implement that goal
-> test and visually verify as required
-> document evidence
-> commit only that cycle's work
-> return to audit current state
-> choose the next improvement
```

Each cycle must be small, reviewable, verified, and committed before the next cycle begins.

## Loop Stop Conditions

Stop and report clearly when any of these happens:

- The user says to stop, pause, or change direction.
- The workspace has dirty or untracked files whose ownership or intended fate is unclear and they block safe work.
- A test, build, static check, PlayMode flow, visual verification, or persistence verification fails and cannot be fixed within the current cycle.
- Unity compilation fails and the root cause is outside the current cycle's scope.
- A required player-facing verification path cannot be automated or observed.
- The next valuable task requires product/design judgment that cannot be inferred from existing docs.
- The next task would be too broad for one reviewable cycle.

When stopped, do not start a new cycle. Summarize current state, completed commits, failed checks, and the exact user decision needed.

## Non-Negotiable Constraints

- Treat unknown dirty or untracked workspace files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated dirty files.
- Do not use `git reset --hard`, `git checkout --`, or destructive cleanup commands.
- Do not edit `Assets/**/*.cs` directly through shell commands. Use Unity MCP script tools for Unity C# files.
- All gameplay/entity work must remain ScriptableObject-driven.
- Do not add production C# branching on entity IDs, route IDs, quest IDs, activity IDs, location IDs, NPC IDs, crop IDs, tool IDs, or similar identifiers.
- Visible gameplay, UI, sprite, tilemap, placement, scene, prefab, camera, and interaction changes are not complete until directly verified through the player-facing PlayMode flow.
- Visible or interactive cycles must capture or inspect Game View or Camera screenshot evidence.
- Tilemap, sprite, placement, scene, prefab, camera, House expansion, construction, and UI persistence work must separately verify saved EditMode asset/scene state when the result should persist.
- Goal-like UI must remain owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not merge unrelated fixes into a cycle just because they are nearby.
- Commit each completed cycle separately with an allowed Korean commit prefix.

## Required Skills

Use these skills as the loop requires:

- `superpowers:brainstorming` at the start of each cycle to select and shape the next improvement.
- `superpowers:writing-plans` when the selected cycle needs multi-step implementation planning.
- `superpowers:executing-plans` or `superpowers:subagent-driven-development` when executing a written plan.
- `superpowers:test-driven-development` for every bugfix or behavior change.
- `superpowers:systematic-debugging` for failures, regressions, unexpected Unity behavior, or unclear dirty state.
- `superpowers:verification-before-completion` before claiming a cycle is complete or committing it.
- `superpowers:requesting-code-review` for broad, risky, or cross-system cycles.
- `superpowers:finishing-a-development-branch` only when the user asks to finish or integrate the branch.

## Cycle 0: Initial Safety Audit

Before selecting the first improvement, run and inspect:

```powershell
git status --short
git log --oneline -n 12
git diff --name-status
git diff --stat
rg --files docs/superpowers/goals docs/superpowers/plans docs/superpowers/audits docs/superpowers/specs
```

Read only the relevant recent docs needed to understand current direction. Prefer the newest files in:

```text
docs/superpowers/goals/
docs/superpowers/plans/
docs/superpowers/audits/
docs/superpowers/specs/
```

If recent work touches visible gameplay, UI, tilemaps, placement, scene, prefab, camera, or interaction systems, inspect recent evidence:

```powershell
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 30 Name,Length,LastWriteTime
```

If unsafe dirty state exists, the first cycle must be workspace triage. Do not begin feature work until the dirty-state risk is resolved or explicitly deferred with a written audit.

## Each Cycle: Candidate Selection

At the start of every cycle, produce three candidate improvements unless safety state forces a triage cycle.

For each candidate, summarize:

- title,
- why it matters now,
- expected player or developer value,
- main risk,
- likely verification burden,
- category: foundation, bugfix, feature, UI, asset, test, network, or docs.

Select one candidate yourself. Do not ask the user to choose unless product intent is genuinely ambiguous or the choice would discard user-owned work.

Prioritize in this order:

1. Workspace safety, dirty-state triage, or generated artifact cleanup.
2. Broken tests, broken player-facing flows, compilation errors, or failed builds.
3. Missing direct visual verification from recent visible or interactive work.
4. Foundation hardening that reduces future feature cost.
5. Small vertical-slice gameplay improvements.
6. UI quality improvements that improve an existing workflow.
7. New feature slices only when the foundation is stable enough.

Do not choose a broad multi-system feature when a smaller verifiable slice can advance the same direction.

## Each Cycle: Per-Cycle Goal Document

Before implementation, write a focused goal document:

```text
docs/superpowers/goals/YYYY-MM-DD-<cycle-topic>-goal.md
```

Use this structure:

```markdown
# Goal: <Title>

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

<One focused outcome.>

## Non-Negotiable Constraints

<Project rules, ownership rules, dirty workspace constraints, and forbidden shortcuts.>

## Required Skills

<Skills used in this execution cycle.>

## Required Work

<Concrete implementation or documentation tasks.>

## Required TDD

<Failing tests first, expected RED, expected GREEN. If documentation-only, explain why TDD is not applicable and what verification replaces it.>

## Required Verification

<Exact Unity tests, scripts, static checks, screenshots, PlayMode player-flow checks, saved asset/scene checks, and persistence checks.>

## Acceptance Criteria

<Observable completion criteria.>

## Commit

<Korean commit prefix and summary guidance.>

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
```

Self-review the per-cycle goal before implementation:

- no `TBD`, `TODO`, or placeholder text,
- no vague "verify as needed" language,
- no direct method-call shortcut presented as the only PlayMode proof,
- exact test method names where known,
- exact screenshot or evidence paths where applicable,
- explicit dirty workspace handling,
- explicit no entity ID branching rule for gameplay/entity work,
- explicit `ObjectiveJournalPanel` ownership rule for goal-like UI work,
- scope small enough for one implementation cycle.

## Each Cycle: Implementation Rules

Follow the per-cycle goal exactly.

For code changes:

- Write or tighten failing tests first.
- Confirm the RED failure is real and related.
- Implement the smallest change that satisfies the goal.
- Keep edits inside the selected cycle's scope.
- For Unity C# files under `Assets/**/*.cs`, use Unity MCP script tools, not shell writes.
- Keep unrelated dirty files untouched.

For documentation-only cycles:

- Create the requested audit, spec, or goal document.
- Verify it has no placeholders or unsupported claims.
- Run static checks appropriate to documentation changes.

For visible or interactive cycles:

- Reproduce the user-facing path in PlayMode.
- Use real input, movement, trigger, UI click/selection, placement, scene transition, or the exact described flow.
- Capture or inspect Game View or Camera screenshot evidence.
- Inspect relevant runtime state.
- Verify saved scene, prefab, ScriptableObject, palette, tilemap, or serialized asset state when persistence is expected.

## Each Cycle: Required Verification

Run verification required by the per-cycle goal. Also run these baseline checks unless impossible or irrelevant:

```powershell
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

For Unity code changes, run the narrowest relevant EditMode or PlayMode tests first, then broader tests if the touched surface is shared.

For visible or interactive changes, automated tests are required but not sufficient. Record the PlayMode route, screenshot path, runtime state inspected, and saved-state inspection result.

Do not claim the cycle is complete until fresh verification output confirms it.

## Each Cycle: Commit Rules

Before committing:

- Re-run the required verification.
- Review `git status --short`.
- Stage only files belonging to the completed cycle.
- Do not stage unrelated dirty/untracked files.
- Use an allowed Korean prefix:
  - `[FEATURE]`
  - `[BUGFIX]`
  - `[REFACTOR]`
  - `[UI]`
  - `[ASSET]`
  - `[BALANCE]`
  - `[TEST]`
  - `[DOCS]`
  - `[CHORE]`
  - `[NETWORK]`
  - `[META]`

Commit each cycle separately. After committing, run:

```powershell
git show --stat --oneline --name-only HEAD
git status --short
```

Then start the next cycle automatically unless a stop condition applies.

## Reporting Cadence

At the end of each cycle, report briefly:

- selected candidate,
- files changed,
- tests and visual checks run,
- evidence paths,
- commit hash and message,
- remaining dirty workspace items,
- next cycle starting point.

Then continue to the next cycle without waiting for user approval unless a stop condition applies.
