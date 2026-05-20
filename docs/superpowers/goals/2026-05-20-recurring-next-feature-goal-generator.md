# Goal: Recurring Next Feature Goal Generator

Paste this prompt into a planning goal session when you want the next ROOTBORN improvement selected and written as an executable goal. This session must write the next goal prompt only. Do not implement the selected feature in this same session.

## Objective

Inspect the current ROOTBORN repository state, identify the next highest-value improvement, and write one focused execution goal under:

```text
docs/superpowers/goals/YYYY-MM-DD-<short-topic>-goal.md
```

The generated goal must be ready for a separate development session to execute with TDD, Unity verification, direct player-facing PlayMode testing, and clean commits.

## Non-Negotiable Constraints

- Do not write production code, edit Unity assets, or change scenes/prefabs in this generator session.
- Do not edit `Assets/**/*.cs` directly through shell commands.
- Treat unknown dirty or untracked workspace files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated dirty files.
- Do not use `git reset --hard`, `git checkout --`, or destructive cleanup commands.
- If the workspace has unsafe dirty state, generate a triage/stabilization goal before any feature goal.
- All generated gameplay/entity work must preserve ScriptableObject-driven design.
- Generated goals must forbid production C# branching on entity IDs, route IDs, quest IDs, activity IDs, location IDs, NPC IDs, crop IDs, tool IDs, or similar identifiers.
- Generated visible or interactive goals must require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Generated tilemap, sprite, placement, scene, prefab, camera, House expansion, construction, or UI goals must require both runtime visual verification and saved EditMode asset/scene verification when persistence is expected.
- Goal-like UI must remain owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Keep each generated goal small enough for one reviewable implementation session.

## Required Skills

Use these skills in this generator session:

- `superpowers:brainstorming`
- `superpowers:verification-before-completion`

The generated execution goal should name the skills needed for its own later session. Use these as appropriate:

- `superpowers:executing-plans`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`
- `superpowers:requesting-code-review`
- `superpowers:finishing-a-development-branch`

## Required Context Audit

Run and inspect:

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

If recent work touches visible gameplay, UI, tilemaps, placement, scene, prefab, camera, or interaction systems, check whether the prior verification evidence exists and is current:

```powershell
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 30 Name,Length,LastWriteTime
```

Do not assume that passing tests prove visible correctness. Check whether the previous goal required screenshots, player input, scene reload, saved asset inspection, or persistence verification.

## Candidate Selection

Produce exactly three candidate next goals unless unsafe workspace state forces a triage goal.

For each candidate, summarize:

- title,
- why it matters now,
- expected player or developer value,
- main risk,
- likely verification burden,
- whether it is foundation, bugfix, feature, UI, asset, test, or network work.

Then recommend one candidate.

Prioritize in this order:

1. Workspace safety, dirty-state triage, or generated artifact cleanup.
2. Broken tests, broken player-facing flows, or missing direct visual verification from recent work.
3. Foundation hardening that reduces future feature cost.
4. Small vertical-slice gameplay improvements.
5. UI quality improvements only when they improve an existing player workflow.
6. New feature slices only when the foundation is clean enough to support them.

Do not choose a broad multi-system feature if a smaller verifiable slice can advance the same direction.

## Required Goal Document Shape

Write one new goal file with this structure:

```markdown
# Goal: <Title>

Paste this prompt into a goal session and execute it as written.

## Objective

<One focused outcome.>

## Non-Negotiable Constraints

<Project rules, ownership rules, dirty workspace constraints, and forbidden shortcuts.>

## Required Skills

<Skills for the execution session.>

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

After this goal is implemented, tested, visually verified where required, documented, and committed, run the Recurring Next Feature Goal Generator again to select the next improvement.
```

## Goal Quality Gate

Before finalizing the new goal document, self-review it for:

- no `TBD`, `TODO`, or placeholder text,
- no vague "verify as needed" language,
- no direct method-call shortcut presented as the only PlayMode proof,
- exact test method names where known,
- exact screenshot or evidence paths where applicable,
- explicit dirty workspace handling,
- explicit no entity ID branching rule for gameplay/entity work,
- explicit `ObjectiveJournalPanel` ownership rule for goal-like UI work,
- scope small enough for one implementation session.

Run:

```powershell
git diff --check
git status --short
```

Report the generated goal path and final workspace status.

## Commit

If committing this generator output or a generated goal document, stage only the new or edited goal/spec documents that belong to this planning session.

Use a Korean commit message with an allowed prefix, for example:

```text
[DOCS] 다음 개선 goal 생성 프롬프트 작성
```

Do not stage unrelated generated evidence, Unity imports, deleted assets, or user-owned dirty files.
