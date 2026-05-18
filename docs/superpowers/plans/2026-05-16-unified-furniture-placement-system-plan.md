# Unified Furniture Placement System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the House-only chair/tile placement path with a shared data-driven furniture placement system that works on House and non-House Tilemap surfaces.

**Architecture:** Add a runtime domain under `Rootborn.Game.Placement` for surface metadata, ScriptableObject furniture definitions, placement transactions, instance registry, rotation, and save DTOs. Existing House UI should become a consumer of this domain instead of placing raw tiles directly; PlayMode tests must exercise the visible UI/input path and inspect Tilemap state.

**Tech Stack:** Unity 6000.3, C#, Tilemap, ScriptableObject assets, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for `Assets/**/*.cs`.

---

### Task 1: Shared Domain Model

**Files:**
- Create: `Assets/Scripts/Game/Placement/FurnitureDefinition.cs`
- Create: `Assets/Scripts/Game/Placement/TilePlacementSurface.cs`
- Create: `Assets/Scripts/Game/Placement/FurniturePlacementTypes.cs`
- Test: `Assets/Tests/EditMode/Placement/FurniturePlacementDomainTests.cs`

- [x] **Step 1: Write failing EditMode tests**

Create tests that assert:
- `FurnitureDefinition` rejects an empty stable id and no tile parts.
- `FurnitureDefinition` resolves a single-tile footprint from tile parts.
- `TilePlacementSurface` exposes `SurfaceId`, ground/object/occupancy/preview Tilemaps, bounds, and placement rule flags.

Run: Unity EditMode test filter `Rootborn.Tests.EditMode.Placement.FurniturePlacementDomainTests`.
Expected before implementation: compile failure for missing `Rootborn.Game.Placement` types.

- [x] **Step 2: Implement minimal domain types**

Use `script-update-or-create` to add the three runtime files. `FurnitureDefinition` must be a `ScriptableObject` with serialized fields for stable id, display name, category, allowed surface ids, footprint cells, tile parts, direction variants, blocks movement, placement preference, and optional unlock token. `TilePlacementSurface` must be a `MonoBehaviour` that references the four required Tilemaps and exposes a stable surface id.

- [x] **Step 3: Run domain tests green**

Run the same EditMode filter.
Expected: all tests in `FurniturePlacementDomainTests` pass with no Console errors.

### Task 2: Atomic Placement Service

**Files:**
- Create: `Assets/Scripts/Game/Placement/FurniturePlacementService.cs`
- Test: `Assets/Tests/EditMode/Placement/FurniturePlacementServiceTests.cs`

- [x] **Step 1: Write failing service tests**

Create tests for:
- single-tile furniture places one object tile and one occupancy cell.
- multi-tile furniture places all parts only when every footprint cell is valid.
- failed multi-tile placement leaves object tilemap and occupancy unchanged.
- blocked cells reject placement.
- rotation uses the same direction data for preview validation and final placement.

Run: Unity EditMode test filter `Rootborn.Tests.EditMode.Placement.FurniturePlacementServiceTests`.
Expected before implementation: compile failure for missing `FurniturePlacementService`.

- [x] **Step 2: Implement minimal service**

Add a service that accepts `TilePlacementSurface`, `FurnitureDefinition`, anchor cell, and direction, validates the full footprint first, then writes all object tiles and occupancy in one transaction. Do not branch on furniture id or asset name.

- [x] **Step 3: Run service tests green**

Run the same EditMode filter.
Expected: service tests pass and failed placements have no partial writes.

### Task 3: Instance Registry, Delete, Move

**Files:**
- Create: `Assets/Scripts/Game/Placement/FurniturePlacementRegistry.cs`
- Test: `Assets/Tests/EditMode/Placement/FurniturePlacementRegistryTests.cs`

- [x] **Step 1: Write failing registry tests**

Create tests for:
- placed instances are stored by generated instance id.
- selecting a cell finds the owning multi-tile furniture instance.
- delete clears all object and occupancy cells.
- move validates the destination before clearing the original cells.
- failed move preserves the original instance and Tilemap state.

Run: Unity EditMode test filter `Rootborn.Tests.EditMode.Placement.FurniturePlacementRegistryTests`.
Expected before implementation: compile failure for missing registry APIs.

- [x] **Step 2: Implement registry**

Store instance id, furniture id, surface id, anchor cell, direction, occupied cells, tile cells, and optional state. Registry methods should call the placement service for write/clear operations and should not know furniture-specific ids.

- [x] **Step 3: Run registry tests green**

Run the same EditMode filter.
Expected: registry tests pass.

### Task 4: Save And Load DTOs

**Files:**
- Create: `Assets/Scripts/Game/Placement/FurniturePlacementSaveData.cs`
- Test: `Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs`

- [x] **Step 1: Write failing save tests**

Create tests for:
- save DTO contains furniture id, surface id, anchor cell, direction, and optional state.
- load rebuilds tilemap state from a catalog lookup.
- load skips missing definitions without crashing.
- repeated load does not duplicate occupied cells or object tiles.

Run: Unity EditMode test filter `Rootborn.Tests.EditMode.Placement.FurniturePlacementSaveTests`.
Expected before implementation: compile failure for missing save DTOs.

- [x] **Step 2: Implement save/load**

Use stable string ids and integer cell coordinates only. Loading should clear prior registry entries for the target surface before replaying saved instances.

- [x] **Step 3: Run save tests green**

Run the same EditMode filter.
Expected: save tests pass.

### Task 5: SO Catalog And Registry Wiring

**Files:**
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Create: `Assets/Data/Interiors/Furniture/`
- Create: sample `FurnitureDefinition` assets for chair and one multi-tile desk/table.
- Test: `Assets/Tests/EditMode/Placement/FurnitureCatalogRegistryTests.cs`

- [x] **Step 1: Write failing catalog tests**

Create tests for:
- `GameDataRegistry` exposes registered `FurnitureDefinition[]`.
- all furniture assets under `Assets/Data/Interiors/Furniture/` are present in `Assets/Data/Registry/GameDataRegistry.asset`.
- no runtime furniture catalog path uses `Resources.LoadAll`.

Run: Unity EditMode test filter `Rootborn.Tests.EditMode.Placement.FurnitureCatalogRegistryTests`.
Expected before implementation: missing registry property or failing asset wiring.

- [x] **Step 2: Implement registry field and assets**

Use Unity MCP or Editor asset tooling for C# and asset generation. Remove the `InteriorFurnitureCatalog.LoadChairFurniture()` Resources dependency from runtime UI consumers.

- [x] **Step 3: Run catalog tests and CI id gate**

Run the catalog EditMode filter and `Scripts/ci/check-no-entity-id-branching.sh`.
Expected: tests pass and CI gate exits 0.

### Task 6: House UI Consumer

**Files:**
- Modify: `Assets/Scripts/UI/Interiors/InteriorFurniturePlacementUi.cs`
- Modify: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- Test: `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs`

- [x] **Step 1: Write failing PlayMode tests**

Extend tests to drive the real House UI path:
- click category.
- click a `FurnitureDefinition` button.
- rotate with user input or the UI control.
- click a valid cell through the same input path as a player.
- assert overlay clears.
- assert delete and move buttons work through UI.

Run: Unity PlayMode test filter `Rootborn.Tests.PlayMode.Interiors.HouseInteriorFurniturePlacementPlayModeTests`.
Expected before implementation: missing UI controls or failed assertions.

- [x] **Step 2: Convert UI to shared placement domain**

The House panel should build furniture buttons from the registry catalog, refresh preview through `FurniturePlacementService`, and place/delete/move through `FurniturePlacementRegistry`.

- [x] **Step 3: Run House PlayMode tests green**

Run the same PlayMode filter.
Expected: tests pass and Console has no relevant errors.

### Task 7: Non-House Surface Verification

**Files:**
- Create: `Assets/Tests/PlayMode/Placement/NonHouseFurnitureSurfacePlayModeTests.cs`
- Runtime setup may create a test-only scene object graph in the test.

- [x] **Step 1: Write failing PlayMode test**

Create a non-House test surface with ground/object/occupancy/preview Tilemaps, attach `TilePlacementSurface`, select furniture through the same palette/placement flow, and place it.

Run: Unity PlayMode test filter `Rootborn.Tests.PlayMode.Placement.NonHouseFurnitureSurfacePlayModeTests`.
Expected before implementation: no reusable UI/surface binding for non-House.

- [x] **Step 2: Add reusable surface binding**

Make the placement UI/service bind to any active `TilePlacementSurface`, not hard-coded `House*Tilemap` names.

- [x] **Step 3: Run non-House PlayMode tests green**

Run the same PlayMode filter.
Expected: non-House surface accepts the same furniture placement path.

### Task 8: Direct Visual Verification And Audit

**Files:**
- Create or update verification notes in the final response only unless a persistent audit artifact is requested.

- [x] **Step 1: Run mandatory automation**

Run:
- `Scripts/ci/check-no-entity-id-branching.sh`
- relevant Placement EditMode filters
- relevant Placement/Interiors PlayMode filters

- [x] **Step 2: Perform player-facing PlayMode flow**

In PlayMode, reproduce:
- open placement UI.
- select category.
- select furniture object.
- verify green/red preview.
- rotate.
- click valid cell.
- select placed furniture.
- delete.
- place again.
- move.
- save and reload.
- repeat on non-House/test surface.

- [x] **Step 3: Capture and inspect visual evidence**

Capture Game View or Camera screenshots and inspect runtime state:
- surface id.
- object tilemap name.
- placed furniture id.
- tile names.
- sprite names.
- occupied cells.
- overlay valid/invalid count.
- decoration/default tile under furniture.
- absence of sample/debug tilemaps.
- absence of default generated furniture under placed object.
- absence of duplicate object tiles after load.

- [x] **Step 4: Completion audit**

Map every requirement in `docs/superpowers/goals/2026-05-16-unified-furniture-placement-system-goal.md` to concrete files, tests, CI output, screenshots, runtime logs, and Console state. Only mark the goal complete if all requirements have direct evidence.
