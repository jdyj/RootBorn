# Modular Portability Refactor Goal Prompt

Paste the following prompt into the goal session and execute it as written.

```md
ROOTBORN Unity project has a completed portability audit at
`docs/superpowers/audits/2026-05-14-modular-portability-audit.md`.

This goal is the first implementation pass after that audit. Convert the highest-priority
portability findings into a tested refactor that makes runtime gameplay data load through
explicit registries/providers instead of editor-only scans or `Resources.LoadAll` fallbacks.

## Mandatory Rules

- Follow `AGENTS.md` and `.claude/rules/*`.
- Do not write `Assets/**/*.cs` directly from disk tools. New or modified C# scripts must go
  through Unity MCP `script-update-or-create` or another Unity Editor-safe path.
- Use TDD. Write failing tests first, confirm the expected failure, then implement.
- Preserve existing dirty work. Do not revert unrelated changes.
- Keep gameplay entities 100% ScriptableObject/data-driven.
- Do not introduce entity-id `if`/`switch` branches, ID enums, or hard-coded content IDs.
- Do not use `Resources.Load`, `Resources.LoadAll`, `AssetDatabase.FindAssets`, scene object
  name searches, or folder scans as runtime gameplay data providers.
- If a compatibility path is needed for editor tooling, isolate it under editor-only code and
  document why it is not a runtime dependency.

## Source Audit Basis

Use the audit's highest-priority follow-up goal:

1. **[REFACTOR][TEST] Registry-only data provider sweep**
   - Remove editor-only folder scans and `Resources.LoadAll` fallbacks for gameplay data.
   - Impact files include:
     - `Assets/Scripts/Game/StudentLife/GameDataRegistryExplorationExtensions.cs`
     - `Assets/Scripts/Game/StudentLife/GameDataRegistryLocationStateExtensions.cs`
     - `Assets/Scripts/Game/WorldState/GameDataRegistryWorldStateUsageExtensions.cs`
     - `Assets/Scripts/Game/DiscoveryClues/GameDataRegistryDiscoveryClueExtensions.cs`
     - `Assets/Scripts/Game/DiscoveryClues/GameDataRegistryClueInterpretationExtensions.cs`
     - `Assets/Scripts/Game/Common/GameDataRegistry.cs`
     - `Assets/Data/Registry/GameDataRegistry.asset`
     - related registry asset tests under `Assets/Tests/EditMode`

Treat other audit follow-ups as out of scope unless they are required to finish this provider
sweep safely.

## Required Deliverables

1. Runtime data provider refactor
   - Exploration interactions, location states, world-state usages, discovery clues, and clue
     interpretations must be obtainable from `GameDataRegistry` or explicit provider SO fields
     in player-compatible runtime code.
   - Runtime code must not return empty arrays merely because `AssetDatabase` is unavailable.
   - Runtime code must not fall back to `Resources.LoadAll`.

2. Registry/schema wiring
   - Add or expose registry fields only as needed for the domains above.
   - Register existing SO assets in `Assets/Data/Registry/GameDataRegistry.asset` or an
     explicit registry/provider asset that is itself reachable from the registry.
   - Preserve existing data paths and SO-driven extension semantics.

3. Tests
   - Add failing EditMode source-audit tests before production changes that catch:
     - `AssetDatabase.FindAssets` in runtime gameplay data providers.
     - `Resources.Load` or `Resources.LoadAll` in runtime gameplay data providers.
     - extension methods that return `Array.Empty<T>()` only because the Unity Editor is absent.
   - Add registry asset tests proving the relevant arrays/providers are populated from
     `GameDataRegistry.asset`.
   - Add behavior tests proving a test-only SO added to the registry is returned without
     changing system code.
   - Keep existing PlayMode scenarios green unless an unrelated existing failure is documented
     with evidence.

4. CI gates
   - Run `Scripts/ci/check-no-entity-id-branching.sh`.
   - Run `Scripts/ci/check-scenario-registry-sync.sh`.
   - Run the focused EditMode tests for the modified registry/provider domains.
   - Run any existing tests that directly cover the changed domains.

5. Final report
   - Include a prompt-to-artifact checklist mapping every requirement in this goal to concrete
     file paths, tests, and command output.
   - Include any verification that could not be run and why.
   - Include remaining portability risks explicitly deferred to later audit follow-up goals.

## Out Of Scope

- Objective Journal provider consolidation.
- Scene installer profile abstraction.
- Network/save boundary adapter refactor.
- Legacy Farm/Crop isolation.
- Broad entity-id CI expansion except for source-audit coverage required by this provider sweep.
- UI visual redesign or new gameplay feature work.

## Completion Conditions

- The runtime provider paths listed above no longer depend on `AssetDatabase.FindAssets`,
  `Resources.Load`, `Resources.LoadAll`, or editor-only folder scans.
- The relevant data is reachable from registry-backed runtime paths in player-compatible code.
- Tests prove registry-only extension with SO data and fail if runtime provider scans return.
- Focused EditMode tests pass.
- `Scripts/ci/check-no-entity-id-branching.sh` passes.
- `Scripts/ci/check-scenario-registry-sync.sh` passes or reports only documented pre-existing
  warnings that are unrelated to this refactor.
- Final report includes the completion checklist and remaining deferred risks.
```
