# Town Gif World Style Full Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the saved Town ground visual style from LDtk-generated terrain tiles to curated GifWorldStyle autotiles without breaking buildings, props, collision, camera, or player traversal.

**Architecture:** Treat `Assets/Scenes/Town.unity` as the saved player-facing scene and `Assets/LDtk/Generated/TownTiles` as the source style to be progressively replaced. The conversion must keep high-sorting building/prop tilemaps intact, replace only ground-family visual cells first, and verify both saved Tilemap references and actual PlayMode rendering.

**Tech Stack:** Unity 6000.3.13f1, Unity Tilemap, LDtk-generated Tile assets, `Ruccho.Utilities.FangAutoTile`, Unity MCP, NUnit EditMode/PlayMode tests.

---

## File Map

- Modify: `Assets/Scenes/Town.unity`
  - Replace selected cells in `Town2LdtkTilemaps/Town2LdtkGroundTilemap` and later `Town2LdtkGround2Tilemap`.
- Modify: `Assets/Tests/EditMode/Visual/TownGifWorldStyleSliceTests.cs`
  - Expand from spawn-slice assertions to named conversion-zone assertions.
- Create: `Assets/Tests/EditMode/Visual/TownGifWorldStyleFullConversionTests.cs`
  - Verifies saved Town ground-family tilemaps use curated Gif tiles in each converted zone and preserve non-ground layers.
- Create: `Assets/Tests/PlayMode/Visual/TownGifWorldStylePlayModeTests.cs`
  - Opens Town in PlayMode and verifies converted zones render through the actual camera.
- Read only unless intentionally migrating later: `Assets/LDtk/Generated/TownTiles/**`
  - Existing LDtk tile assets remain source reference and should not be edited.

---

### Task 1: Lock the Current Expanded Spawn Slice

**Files:**
- Modify: `Assets/Tests/EditMode/Visual/TownGifWorldStyleSliceTests.cs`

- [ ] **Step 1: Keep the expanded spawn-zone test**

The current test should assert:

```text
Scene: Assets/Scenes/Town.unity
Tilemap: Town2LdtkTilemaps/Town2LdtkGroundTilemap
Region: x=5..18, y=-18..-12
Expected: 56 sand_edge cells, 42 grass_base cells
```

- [ ] **Step 2: Run EditMode visual tests**

Run:

```powershell
@'
{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Visual","includePassingTests":true,"includeMessages":true,"includeStacktrace":false,"includeLogs":false}
'@ | unity-mcp-cli.cmd run-tool tests-run --input-file -
```

Expected: all visual EditMode tests pass.

---

### Task 2: Classify Town Ground Zones

**Files:**
- Create: `Builds/Logs/town-ground-zone-audit.json`

- [ ] **Step 1: Inspect ground-family tilemaps**

Run an MCP `script-execute` audit over:

```text
Town2LdtkTilemaps/Town2LdtkGroundTilemap
Town2LdtkTilemaps/Town2LdtkGround2Tilemap
Town2LdtkTilemaps/Town2LdtkBeachTilemap
```

Record:

```json
{
  "tilemap": "Town2LdtkGroundTilemap",
  "occupiedCells": 5050,
  "bounds": "...",
  "uniqueTileAssetPaths": [
    "Assets/LDtk/Generated/TownTiles/..."
  ]
}
```

- [ ] **Step 2: Do not touch non-ground layers**

Explicitly exclude these from the first full conversion:

```text
Town2LdtkBuildingTilemap
Town2LdtkBuildingOverdraw01Tilemap
Town2LdtkHospitalTilemap
Town2LdtkPoliceTilemap
Town2LdtkSchoolTilemap
Town2LdtkShopTilemap
Town2LdtkPropsTilemap
Town2LdtkPropsOverdraw01Tilemap
Town2LdtkPropsOverdraw02Tilemap
Town2LdtkFenseTilemap
Town2LdtkFenseOverdraw01Tilemap
```

---

### Task 3: Convert Zone by Zone

**Files:**
- Modify: `Assets/Scenes/Town.unity`
- Modify: `Assets/Tests/EditMode/Visual/TownGifWorldStyleSliceTests.cs`

- [ ] **Step 1: Add a RED test for one named zone**

Start with one rectangular zone at a time. Example:

```text
Zone name: TownSpawnNeighborhood
Region: x=0..24, y=-24..-8
Expected tile policy:
  grass_base for open grass border cells
  sand_edge for path cells
  stone_edge for paved/city cells only where visually confirmed
```

Expected first run: fail on cells outside the current expanded spawn slice.

- [ ] **Step 2: Apply only that zone**

Use an MCP `script-execute` editor script that:

```csharp
var scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);
var tilemap = GameObject.Find("Town2LdtkTilemaps/Town2LdtkGroundTilemap").GetComponent<Tilemap>();
tilemap.SetTile(position, chosenCuratedGifTile);
EditorSceneManager.SaveScene(scene, "Assets/Scenes/Town.unity");
```

- [ ] **Step 3: Run EditMode tests**

Expected: the named-zone test passes.

- [ ] **Step 4: Run PlayMode visual verification**

Open Town, enter PlayMode, position camera on the converted zone, capture:

```text
Builds/Logs/town-gif-zone-<zone-name>.png
```

Expected: no blank cells, no magenta sprites, no stale debug tilemaps, no hidden result under overdraw layers.

---

### Task 4: Preserve Traversal and Scene Layering

**Files:**
- Create: `Assets/Tests/PlayMode/Visual/TownGifWorldStylePlayModeTests.cs`

- [ ] **Step 1: Add PlayMode smoke test**

The test should:

```text
1. Load Assets/Scenes/Town.unity in PlayMode
2. Assert Main Camera exists and is orthographic
3. Assert Town2LdtkGroundTilemap TilemapRenderer is enabled
4. Assert converted-zone cells still contain curated Gif tiles
5. Assert building/prop tilemaps still have their original nonzero cell counts
```

- [ ] **Step 2: Run PlayMode tests**

Run:

```powershell
@'
{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Visual","includePassingTests":true,"includeMessages":true,"includeStacktrace":false,"includeLogs":false}
'@ | unity-mcp-cli.cmd run-tool tests-run --input-file -
```

Expected: no visual PlayMode regressions.

---

### Task 5: Full Ground Conversion Gate

**Files:**
- Modify: `Assets/Scenes/Town.unity`
- Modify tests from previous tasks.

- [ ] **Step 1: Convert remaining ground cells only after at least two zones pass visual review**

Do not convert all 5050 cells in one blind script. Require two visually verified zones first:

```text
TownSpawnNeighborhood
TownParkOrRoadSample
```

- [ ] **Step 2: Apply full ground-family conversion**

Replace only ground-family tilemaps:

```text
Town2LdtkGroundTilemap
Town2LdtkGround2Tilemap
Town2LdtkBeachTilemap
```

Keep non-ground tilemaps untouched.

- [ ] **Step 3: Verify saved state**

Expected saved-state evidence:

```text
Ground-family curated Gif cell count > 4500
Non-ground tilemap cell counts unchanged from audit
No source Resources dependency introduced
Scene dirty false after save
```

- [ ] **Step 4: Verify player-facing visuals**

Capture at least three PlayMode screenshots:

```text
Builds/Logs/town-gif-full-spawn.png
Builds/Logs/town-gif-full-shop.png
Builds/Logs/town-gif-full-hospital.png
```

Expected: converted ground is visible and coherent under buildings/props.

---

## Risks

- LDtk regeneration may overwrite manual saved-scene Tilemap edits. If the project still regenerates Town from LDtk, the final conversion should become a repeatable editor importer step rather than one-off scene edits.
- `FangAutoTile` may visually connect differently when surrounded by untouched LDtk tiles. Zones should be expanded with clear boundaries or converted in contiguous regions.
- Day/night lighting can make screenshots dark. Do not disable lighting for final verification; use it as the player-facing state, but note the time-of-day state in the report.

