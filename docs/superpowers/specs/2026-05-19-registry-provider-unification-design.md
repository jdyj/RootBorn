# Registry Provider Unification Design

## Purpose

ROOTBORN runtime systems currently read `GameDataRegistry` through several paths: `Managers.Data.Registry`, Addressables via `DataManager`, `Resources.Load<GameDataRegistry>("GameDataRegistry")`, and editor-only `AssetDatabase` fallbacks inside runtime installers. This creates different registry origins across PlayMode, player builds, dedicated server builds, and tests.

This design unifies runtime registry access behind one provider API, reduces direct fallback usage, and adds player-facing PlayMode and QA simulation coverage so the refactor is proven through the same flow players use.

## Scope

The first implementation slice introduces a single runtime registry access surface and migrates a small set of high-risk callers. It does not split `GameDataRegistry` into domain registries and does not delete `Assets/Resources/GameDataRegistry.asset` yet.

Target migration candidates:

- `CampaignRuntimeInstaller`
- `DailyEventRuntimeInstaller`
- `FarmCharacterHudOverlayInstaller`
- `SeededFarmWorldApplier`
- `FarmAutoFiller`

These callers are chosen because they cover Town student-life UI, day/event runtime wiring, Farm HUD/world generation, and legacy fallback code that currently appears in the static usage allowlist.

## Architecture

Add a lightweight `GameDataRegistryProvider` in runtime code. The provider exposes one API for obtaining the active registry:

```csharp
public interface IGameDataRegistryProvider
{
    bool TryGet(out GameDataRegistry registry);
    GameDataRegistry Require();
}
```

The default provider resolves registry data in this priority order:

1. Explicit test/runtime override.
2. `Managers.Data.Registry`, when initialized.
3. The `DataManager` Addressables-backed registry source.
4. Editor-only asset loading, only in Editor-specific code paths or tests.

Direct runtime `Resources.Load<GameDataRegistry>` is not part of the target steady state. If a temporary compatibility fallback is needed during the first slice, it must be isolated inside the provider, logged as legacy fallback, and covered by a test that prevents new direct call sites.

Consumers should depend on the provider, not on file paths. Existing data-driven entity rules still apply: no activity, quest, clue, career, tool, or house-stage ID branching is introduced.

## Data Flow

`GameBootstrap` or the existing manager initialization keeps loading the registry through `DataManager`. Runtime installers then ask the provider for the active registry instead of repeating fallback logic.

Typical flow:

1. App boot initializes `Managers.Data`.
2. `DataManager.InitAsync` resolves `GameDataRegistry`.
3. Runtime installers call `GameDataRegistryProvider.Current.TryGet`.
4. Domain systems bind SO arrays from the same registry instance.
5. Tests can install an override provider or registry instance and reset it in teardown.

This keeps registry origin deterministic while preserving existing scene/runtime installer behavior.

## PlayMode Scenario

Add a focused PlayMode E2E test for the provider migration. It must use the player-facing path:

1. Load `MainMenu`.
2. Click the real save-slot `NewGameButton`.
3. Confirm SPUM character creation through the UI.
4. Enter `Town`.
5. Verify the active registry came through the unified provider.
6. Verify migrated systems render or bind data from that same registry:
   - Campaign HUD or campaign runtime state exists.
   - Daily/student-life runtime data resolves from registry-backed SO arrays.
   - At least one location, NPC, activity, or day-result interaction still works through player input.
7. Save and reload through the save-slot UI.
8. Verify registry-backed UI/progress still rebuilds after reload.
9. Capture a Game View screenshot showing the player-facing UI state.

The test must not pass by directly invoking internal completion methods. It may inspect runtime state after the actual UI/input path has produced that state.

## QA Simulation

Add a focused script, for example:

```powershell
Scripts\qa\run-registry-provider-playmode-simulation.ps1
```

The script should:

- Accept `-RunName` and optional `-SaveSlot`.
- Run provider EditMode tests.
- Run migrated caller tests.
- Run the new provider PlayMode E2E test.
- Run `Scripts/ci/check-no-entity-id-branching.sh`.
- Write logs and screenshots under `production/qa/evidence/<run-name>/` or `Builds/Logs/<run-name>/`.
- Fail nonzero if any required test or gate fails.

The script is a focused local/release validation aid. It does not replace broad CI or nightly multiplayer gates.

## Static Gates

Update `StaticRuntimeUsageGateTests` as migrations land:

- Remove migrated `Resources.Load<GameDataRegistry>` allowlist entries.
- Keep non-registry `Resources.Load` entries, such as font loading, out of this slice unless directly related.
- Add a provider-specific assertion that runtime code does not add new direct `Resources.Load<GameDataRegistry>` call sites outside the provider compatibility path.

The existing entity-branching shell gate remains mandatory.

## Testing

Required tests for the implementation plan:

- EditMode: provider returns `Managers.Data.Registry` when available.
- EditMode: provider override is isolated and resettable for tests.
- EditMode: provider fails clearly when no registry source exists.
- EditMode: migrated runtime installers no longer contain direct registry `Resources.Load` fallback.
- PlayMode: MainMenu to Town provider scenario passes through real UI/input flow.
- PlayMode: save/load rebuilds provider-backed UI state.
- QA script: focused provider simulation passes and records evidence.
- CI gate: `Scripts/ci/check-no-entity-id-branching.sh` passes.

## Performance

The provider must not scan assets, scenes, or all loaded objects per frame. Resolution should be cached after successful lookup and invalidated only by explicit test reset, manager reset, or scene/bootstrap lifecycle events that already exist.

Runtime installers should ask for the registry at initialization or explicit refresh points. They should not poll the provider in `Update`.

## Risks

The main risk is bootstrap order. Some installers may run before `Managers.Data` is initialized. The provider should make that state explicit with `TryGet` failure or a controlled initialization wait, instead of silently loading a different registry.

Another risk is test cross-contamination from provider overrides. Tests must reset provider state in teardown.

Legacy Farm/Crop systems still use older registry/resource paths. The first slice should reduce direct runtime registry fallback without pretending the whole legacy path is removed.

## Completion Criteria

The slice is complete when:

- The provider exists and migrated callers use it.
- Direct registry fallback allowlist entries shrink for the migrated files.
- Required EditMode and PlayMode tests pass.
- The focused QA simulation script passes.
- The entity branching gate passes.
- Game View screenshot evidence is captured for the player-facing PlayMode flow.
- Remaining legacy registry paths are documented as follow-up work, not hidden as completed cleanup.
