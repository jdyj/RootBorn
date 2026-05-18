# House Furniture Placement Visual Bugfix And Camera Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the user-visible House furniture placement bugs for 1x1 desk selection, blackChair placement cleanup/alignment, and placement-mode camera zoom/pan, with direct PlayMode visual verification.

**Architecture:** Keep the existing shared placement domain (`FurnitureDefinition`, `TilePlacementSurface`, `FurniturePlacementService`, `FurniturePlacementRegistry`) and repair the data/UI consumers around it. Add a small placement camera controller that takes ownership only while the furniture panel is active and restores `CameraFollow` afterwards. All furniture-specific differences stay in ScriptableObject data and registry wiring, not runtime id branches.

**Tech Stack:** Unity 6000.3, C#, Tilemap, ScriptableObject assets, Unity Test Framework EditMode/PlayMode, Unity MCP `script-update-or-create` for `Assets/**/*.cs`, Unity MCP scene/screenshot tools for direct visual verification.

---

## Current Evidence

Relevant current state from inspection:

- `Assets/Data/Interiors/Furniture/Furniture_Desk_Large.asset` has `_stableId: desk.large`, two `_tileParts`, and the second tile is `ShadowlessOffice_Computer_Monitor_r44_c08`.
- `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs` currently expects `PaletteFurniture_desk.large` and asserts that the computer tile appears at `Vector3Int.right`.
- `Assets/Data/Interiors/Furniture/Furniture_Chair_Black.asset` is already a 1x1 definition using `BlackChair_00`.
- `InteriorPlacementPreviewOverlay.ClearOverlay()` clears placement/preview tilemaps, but the user-reported second-chair stale visual must be verified through the player-facing mouse path and runtime Tilemap inventory, not only helper methods.
- `CameraFollow` has no explicit pause/resume API, so placement camera control needs a clean ownership boundary.

## File Structure

- Modify: `Assets/Tests/EditMode/Placement/FurnitureCatalogRegistryTests.cs`
  - Add asset-level assertions that the regular desk furniture exposed to players is 1x1 and contains no computer tile.
- Modify: `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs`
  - Replace the current `desk.large` user-flow expectation with a 1x1 desk user-flow test and keep multi-tile desk as a separate explicit object if it remains.
- Modify: `Assets/Tests/PlayMode/Interiors/FurniturePlacementMouseFlowPlayModeTests.cs`
  - Add real mouse flow coverage for placing blackChair twice and checking stale overlays/debug sample objects.
- Create: `Assets/Tests/EditMode/Interiors/PlacementCameraControllerTests.cs`
  - Test zoom clamp, pan clamp, full-view framing, and `CameraFollow` restore behavior.
- Create: `Assets/Tests/PlayMode/Interiors/HousePlacementCameraControlPlayModeTests.cs`
  - Test player-facing zoom/pan/full-view controls while the House furniture placement panel is active.
- Create: `Assets/Scripts/UI/Interiors/HousePlacementCameraController.cs`
  - Owns placement-mode camera pan/zoom, bounds clamp, full-view framing, and temporary `CameraFollow` disable/restore.
- Modify: `Assets/Scripts/UI/Interiors/InteriorFurniturePlacementUi.cs`
  - Attach/bind the camera controller while the panel is active, add compact controls or shortcuts, and release camera ownership on disable.
- Modify: `Assets/Data/Interiors/Furniture/`
  - Add or correct a player-facing 1x1 desk definition. Keep `desk.large` only if it is explicitly named/presented as a desk+computer or large desk object.
- Modify: `Assets/Data/Registry/GameDataRegistry.asset`
  - Register the corrected/new furniture definition.
- Inspect/possibly modify: `Assets/Scenes/House.unity`
  - Remove or disable stale sample/debug tilemaps and verify saved scene state if scene changes are required.

## Task 1: Desk Catalog Regression Tests

**Files:**
- Modify: `Assets/Tests/EditMode/Placement/FurnitureCatalogRegistryTests.cs`
- Inspect: `Assets/Data/Interiors/Furniture/Furniture_Desk_Large.asset`
- Inspect: `Assets/Data/Registry/GameDataRegistry.asset`

- [ ] **Step 1: Add failing EditMode tests for player-facing desk semantics**

Use `script-update-or-create` only if editing the C# test through Unity MCP. Add this test class content to `FurnitureCatalogRegistryTests`:

```csharp
[Test]
public void PlayerFacingDesk_IsSingleTileAndDoesNotIncludeComputerTile()
{
    var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
    Assert.IsNotNull(registry);

    var desks = registry.FurnitureDefinitions
        .Where(definition => definition != null && definition.Category == "desk")
        .ToArray();

    var regularDesk = desks.SingleOrDefault(definition => definition.StableId == "desk.basic");
    Assert.IsNotNull(regularDesk, "The player-facing regular desk must be a distinct 1x1 furniture definition named desk.basic.");
    Assert.AreEqual(1, regularDesk.FootprintCells.Count, "Regular desk must occupy exactly one cell.");
    Assert.AreEqual(Vector2Int.zero, regularDesk.FootprintCells[0]);
    Assert.AreEqual(1, regularDesk.TileParts.Count, "Regular desk must render exactly one tile part.");

    var tileName = regularDesk.TileParts[0].Tile != null ? regularDesk.TileParts[0].Tile.name : string.Empty;
    StringAssert.DoesNotContain("Computer", tileName, "Regular desk must not include a computer tile part.");
}

[Test]
public void DeskComputerCombination_IsNotNamedAsRegularDesk()
{
    var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
    var deskComputer = registry.FurnitureDefinitions.SingleOrDefault(definition => definition != null && definition.StableId == "desk.large");

    Assert.IsNotNull(deskComputer, "Existing desk.large may remain as a separate multi-tile object.");
    Assert.Greater(deskComputer.FootprintCells.Count, 1);
    StringAssert.Contains("Computer", string.Join(",", deskComputer.TileParts.Select(part => part.Tile != null ? part.Tile.name : string.Empty)));
    StringAssert.DoesNotContain("Basic", deskComputer.DisplayName, "Multi-tile desk+computer must not be labeled as the regular 1x1 desk.");
}
```

- [ ] **Step 2: Run the focused EditMode tests and confirm failure**

Run with Unity MCP:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Placement.FurnitureCatalogRegistryTests includeMessages=true
```

Expected before data repair: `PlayerFacingDesk_IsSingleTileAndDoesNotIncludeComputerTile` fails because `desk.basic` does not exist or regular desk is still mapped to `desk.large`.

- [ ] **Step 3: Repair desk data through Unity asset APIs**

Use Unity MCP asset tooling or an editor utility, not shell writes, to create or update:

- `Assets/Data/Interiors/Furniture/Furniture_Desk_Basic.asset`
- stable id: `desk.basic`
- display name: `Basic Desk`
- category: `desk`
- allowed surfaces: `house`, `test-surface`, `town-test`
- tile parts: one part at local cell `(0, 0)`, using `Assets/Data/Interiors/TileSets/ShadowlessFurnitureTiles/ShadowlessOffice_Desk_Table_r39_c01.asset`
- footprint cells: one cell `(0, 0)`
- blocks movement: `true`
- placement preference: `Any`

Register the asset in `Assets/Data/Registry/GameDataRegistry.asset`.

- [ ] **Step 4: Run the focused EditMode tests green**

Run:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Placement.FurnitureCatalogRegistryTests includeMessages=true
```

Expected: all tests in `FurnitureCatalogRegistryTests` pass.

## Task 2: Desk Player-Facing Placement Flow

**Files:**
- Modify: `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs`
- Modify if needed: `Assets/Scripts/UI/Interiors/InteriorFurniturePlacementUi.cs`
- Modify if needed: `Assets/Scripts/UI/Interiors/InteriorFurnitureCatalog.cs`

- [ ] **Step 1: Replace the old regular desk PlayMode expectation**

In `HouseInteriorFurniturePlacementPlayModeTests`, add constants:

```csharp
private const string BasicDeskButtonName = "PaletteFurniture_desk.basic";
private const string BasicDeskFurnitureId = "desk.basic";
private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";
private const string ComputerTileName = "ShadowlessOffice_Computer_Monitor_r44_c08";
```

Add this test:

```csharp
[UnityTest]
public IEnumerator SelectedBasicDeskFurniture_PlacesOneByOneDeskWithoutComputerTile()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    Assert.IsNotNull(GameObject.Find(BasicDeskButtonName), "The regular player-facing desk button must select desk.basic, not desk.large.");
    GameObject.Find(BasicDeskButtonName).GetComponent<Button>().onClick.Invoke();
    yield return null;

    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
    Assert.IsNotNull(overlay);
    Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
    yield return null;

    StringAssert.Contains(BasicDeskFurnitureId, message);
    var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
    var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
    Assert.IsNotNull(furnitureTilemap);
    Assert.IsNotNull(occupancyTilemap);
    Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName), "Regular desk placement must render exactly one desk tile.");
    Assert.AreEqual(0, CountTiles(furnitureTilemap, ComputerTileName), "Regular desk placement must not attach a computer tile.");
    Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var deskCell));
    Assert.IsNotNull(occupancyTilemap.GetTile(deskCell));
}
```

Keep a separate explicit multi-tile test only if `PaletteFurniture_desk.large` remains visible and clearly named as large/combined. Rename that test to:

```csharp
public IEnumerator SelectedLargeDeskFurniture_PlacesExplicitDeskComputerCombination()
```

- [ ] **Step 2: Run the focused PlayMode test and confirm failure**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HouseInteriorFurniturePlacementPlayModeTests includeMessages=true
```

Expected before UI/data repair: the new basic desk button is missing, or the placed tilemap still contains the computer tile.

- [ ] **Step 3: Fix palette exposure if needed**

If the palette already builds from registry order, the new `desk.basic` asset may appear automatically. If `desk.large` appears before `desk.basic` and user expectations require the regular desk first, update `InteriorFurnitureCatalog.LoadFurniture` ordering to prefer smaller footprints within category:

```csharp
return registry.FurnitureDefinitions
    .Where(definition => definition != null)
    .OrderBy(definition => definition.Category, StringComparer.Ordinal)
    .ThenBy(definition => definition.FootprintCells.Count)
    .ThenBy(definition => definition.StableId, StringComparer.Ordinal)
    .Select(Convert)
    .Where(definition => definition != null)
    .ToList();
```

Do not add id-specific branching.

- [ ] **Step 4: Run the desk PlayMode tests green**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HouseInteriorFurniturePlacementPlayModeTests includeMessages=true
```

Expected: basic desk places one desk tile, no computer tile; explicit large desk test still passes if retained.

## Task 3: blackChair Double-Placement Visual Cleanup

**Files:**
- Modify: `Assets/Tests/PlayMode/Interiors/FurniturePlacementMouseFlowPlayModeTests.cs`
- Modify if needed: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- Inspect/possibly modify: `Assets/Scenes/House.unity`

- [ ] **Step 1: Add real mouse-flow double placement regression**

Add this PlayMode test to `FurniturePlacementMouseFlowPlayModeTests`:

```csharp
[UnityTest]
public IEnumerator BlackChairMousePlacementTwice_DoesNotRevealStaleOverlayOrSampleTilesBehindFirstChair()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Debug/sample chair tilemap must not be visible in playable House.");

    var button = GameObject.Find(ChairButtonName);
    Assert.IsNotNull(button);
    ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    yield return null;

    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
    var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
    var camera = Camera.main;
    Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var firstCell, out var firstScreen));

    var mouse = InputSystem.AddDevice<Mouse>();
    mouse.MakeCurrent();
    var firstDriver = new GameObject("FurnitureFirstChairMouseClickDriver").AddComponent<MouseClickFrameDriver>();
    firstDriver.Configure(mouse, firstScreen);
    yield return null;
    yield return null;

    var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
    Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var firstFurnitureCell));
    Assert.AreEqual(firstCell, firstFurnitureCell);
    Assert.AreEqual(0, overlay.ValidCellCount);
    Assert.AreEqual(0, overlay.InvalidCellCount);

    ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    yield return null;
    Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var secondCell, out var secondScreen));
    Assert.AreNotEqual(firstCell, secondCell, "Second placement should use a different visible valid cell for this regression.");

    var secondDriver = new GameObject("FurnitureSecondChairMouseClickDriver").AddComponent<MouseClickFrameDriver>();
    secondDriver.Configure(mouse, secondScreen);
    yield return null;
    yield return null;
    InputSystem.RemoveDevice(mouse);

    Assert.AreEqual(2, CountTiles(furnitureTilemap, ChairTileName), "Two chair placements should render exactly two selected blackChair tiles.");
    Assert.AreEqual(0, overlay.ValidCellCount, "Overlay must be clear after second placement.");
    Assert.AreEqual(0, overlay.InvalidCellCount, "Overlay must be clear after second placement.");

    var decorationTilemap = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
    Assert.IsNotNull(decorationTilemap);
    Assert.IsNull(decorationTilemap.GetTile(firstFurnitureCell), "First chair cell must not reveal a default/generated chair behind the selected chair after a second placement.");
}
```

- [ ] **Step 2: Run the focused PlayMode test and confirm failure or pass**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.FurniturePlacementMouseFlowPlayModeTests includeMessages=true
```

Expected: if the stale visual exists, the new double-placement test fails with the stale layer or overlay count. If it passes, still perform direct visual verification in Task 8 because the user report is visual.

- [ ] **Step 3: Fix actual stale layer cause**

If overlay counts remain after placement, change `InteriorPlacementPreviewOverlay.ClearOverlay()` so it also clears `_overlay` and any active `TilemapRenderer` named with `PlacementAvailability` or `FurniturePreview` before returning.

If `HouseDecorationTilemap` contains a generated chair under placed furniture after second placement, ensure `ClearDefaultDecorationTiles(anchorCell)` is called for every placed footprint cell after every successful placement and load.

If a saved scene sample tilemap exists, remove or disable that GameObject in `Assets/Scenes/House.unity` through Unity scene tooling and verify the saved scene afterward.

- [ ] **Step 4: Run the blackChair PlayMode tests green**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.FurniturePlacementMouseFlowPlayModeTests includeMessages=true
```

Expected: the double-placement test passes, no sample/debug tilemap is found, and first chair decoration cell is empty.

## Task 4: blackChair Alignment Evidence

**Files:**
- Modify: `Assets/Tests/EditMode/Placement/FurnitureCatalogRegistryTests.cs`
- Modify: `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs`

- [ ] **Step 1: Add asset-level blackChair shape test**

Add to `FurnitureCatalogRegistryTests`:

```csharp
[Test]
public void BlackChair_IsOneByOneAndUsesSingleRenderableTile()
{
    var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
    var chair = registry.FurnitureDefinitions.SingleOrDefault(definition => definition != null && definition.StableId == "chair.black");

    Assert.IsNotNull(chair);
    CollectionAssert.AreEqual(new[] { Vector2Int.zero }, chair.FootprintCells);
    Assert.AreEqual(1, chair.TileParts.Count);
    Assert.AreEqual(Vector2Int.zero, chair.TileParts[0].LocalCell);
    Assert.AreEqual("BlackChair_00", chair.TileParts[0].Tile.name);
}
```

- [ ] **Step 2: Add runtime cell-center evidence**

Add to `HouseInteriorFurniturePlacementPlayModeTests`:

```csharp
[UnityTest]
public IEnumerator SelectedChairFurniture_RendersOnSelectedCellWithoutOffsetCompensation()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
    yield return null;

    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
    Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
    yield return null;

    var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
    Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var chairCell));
    var tile = furnitureTilemap.GetTile(chairCell);
    Assert.IsNotNull(tile);
    Assert.AreEqual("BlackChair_00", tile.name);
    Assert.AreEqual(Vector3.zero, furnitureTilemap.tileAnchor, "Furniture Tilemap should not use an ad hoc visual offset for blackChair.");
}
```

- [ ] **Step 3: Run focused EditMode and PlayMode tests**

Run:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Placement.FurnitureCatalogRegistryTests includeMessages=true
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HouseInteriorFurniturePlacementPlayModeTests includeMessages=true
```

Expected: tests pass if blackChair is correctly defined and Tilemap anchor is stable. If they fail, fix the asset definition or Tilemap anchor through Unity APIs, then rerun.

## Task 5: Placement Camera Controller Tests

**Files:**
- Create: `Assets/Tests/EditMode/Interiors/PlacementCameraControllerTests.cs`
- Create: `Assets/Scripts/UI/Interiors/HousePlacementCameraController.cs`
- Modify: `Assets/Scripts/Game/Player/CameraFollow.cs` only if a public pause API is necessary

- [ ] **Step 1: Write failing EditMode tests for camera math and ownership**

Create `Assets/Tests/EditMode/Interiors/PlacementCameraControllerTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.UI.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class PlacementCameraControllerTests
    {
        [Test]
        public void ClampOrthographicSize_StaysWithinConfiguredRange()
        {
            Assert.AreEqual(4f, HousePlacementCameraController.ClampOrthographicSize(2f, 4f, 12f));
            Assert.AreEqual(12f, HousePlacementCameraController.ClampOrthographicSize(16f, 4f, 12f));
            Assert.AreEqual(8f, HousePlacementCameraController.ClampOrthographicSize(8f, 4f, 12f));
        }

        [Test]
        public void ClampPosition_StaysInsideBoundsForViewport()
        {
            var bounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(20f, 12f, 1f));
            var clamped = HousePlacementCameraController.ClampPosition(new Vector3(20f, 20f, -10f), bounds, 4f, 16f / 9f);

            Assert.LessOrEqual(clamped.x, bounds.max.x);
            Assert.LessOrEqual(clamped.y, bounds.max.y);
            Assert.AreEqual(-10f, clamped.z);
        }

        [Test]
        public void FullViewSize_FramesBoundsWithPadding()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 1f));
            var size = HousePlacementCameraController.CalculateFullViewOrthographicSize(bounds, 16f / 9f, 1.08f);

            Assert.GreaterOrEqual(size, 10f * 0.5f * 1.08f);
            Assert.GreaterOrEqual(size, (20f / (16f / 9f)) * 0.5f * 1.08f);
        }
    }
}
```

- [ ] **Step 2: Run the new EditMode tests and confirm compile failure**

Run:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Interiors.PlacementCameraControllerTests includeMessages=true
```

Expected: compile/test failure because `HousePlacementCameraController` does not exist.

- [ ] **Step 3: Implement `HousePlacementCameraController`**

Use `script-update-or-create` to create `Assets/Scripts/UI/Interiors/HousePlacementCameraController.cs`:

```csharp
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.UI.Interiors
{
    public sealed class HousePlacementCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _minOrthographicSize = 4f;
        [SerializeField] private float _maxOrthographicSize = 14f;
        [SerializeField] private float _zoomStep = 1f;
        [SerializeField] private float _panUnitsPerSecond = 10f;
        [SerializeField] private float _fullViewPadding = 1.08f;

        private CameraFollow _follow;
        private bool _restoreFollowEnabled;
        private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));

        public void Configure(Camera camera, Bounds bounds)
        {
            _camera = camera;
            _bounds = bounds;
            if (_camera != null)
            {
                _follow = _camera.GetComponent<CameraFollow>();
            }
        }

        public void BeginPlacementControl()
        {
            if (_follow != null)
            {
                _restoreFollowEnabled = _follow.enabled;
                _follow.enabled = false;
            }
        }

        public void EndPlacementControl()
        {
            if (_follow != null)
            {
                _follow.enabled = _restoreFollowEnabled;
            }
        }

        public void FullView()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            _camera.orthographicSize = ClampOrthographicSize(CalculateFullViewOrthographicSize(_bounds, _camera.aspect, _fullViewPadding), _minOrthographicSize, _maxOrthographicSize);
            _camera.transform.position = new Vector3(_bounds.center.x, _bounds.center.y, _camera.transform.position.z);
            _camera.transform.position = ClampPosition(_camera.transform.position, _bounds, _camera.orthographicSize, _camera.aspect);
        }

        private void Update()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pan = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) Zoom(-_zoomStep);
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) Zoom(_zoomStep);
                if (keyboard.fKey.wasPressedThisFrame) FullView();
            }

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Zoom(scroll > 0f ? -_zoomStep : _zoomStep);
                }
            }

            if (pan.sqrMagnitude > 0f)
            {
                var delta = new Vector3(pan.normalized.x, pan.normalized.y, 0f) * (_panUnitsPerSecond * Time.unscaledDeltaTime);
                _camera.transform.position = ClampPosition(_camera.transform.position + delta, _bounds, _camera.orthographicSize, _camera.aspect);
            }
        }

        private void Zoom(float delta)
        {
            _camera.orthographicSize = ClampOrthographicSize(_camera.orthographicSize + delta, _minOrthographicSize, _maxOrthographicSize);
            _camera.transform.position = ClampPosition(_camera.transform.position, _bounds, _camera.orthographicSize, _camera.aspect);
        }

        public static float ClampOrthographicSize(float size, float min, float max)
        {
            return Mathf.Clamp(size, Mathf.Min(min, max), Mathf.Max(min, max));
        }

        public static float CalculateFullViewOrthographicSize(Bounds bounds, float aspect, float padding)
        {
            var safeAspect = Mathf.Max(0.01f, aspect);
            var halfHeight = bounds.size.y * 0.5f;
            var halfWidthAsHeight = bounds.size.x / safeAspect * 0.5f;
            return Mathf.Max(halfHeight, halfWidthAsHeight) * Mathf.Max(1f, padding);
        }

        public static Vector3 ClampPosition(Vector3 position, Bounds bounds, float orthographicSize, float aspect)
        {
            var halfHeight = Mathf.Max(0f, orthographicSize);
            var halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            var minX = bounds.min.x + halfWidth;
            var maxX = bounds.max.x - halfWidth;
            var minY = bounds.min.y + halfHeight;
            var maxY = bounds.max.y - halfHeight;
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
            return position;
        }
    }
}
```

- [ ] **Step 4: Run camera EditMode tests green**

Run:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Interiors.PlacementCameraControllerTests includeMessages=true
```

Expected: all camera math tests pass.

## Task 6: Bind Camera Controls To House Placement UI

**Files:**
- Modify: `Assets/Scripts/UI/Interiors/InteriorFurniturePlacementUi.cs`
- Modify: `Assets/Tests/PlayMode/Interiors/HousePlacementCameraControlPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode camera controls test**

Create `Assets/Tests/PlayMode/Interiors/HousePlacementCameraControlPlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HousePlacementCameraControlPlayModeTests
    {
        [UnityTest]
        public IEnumerator HousePlacementPanel_AttachesCameraControllerAndFullViewButton()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var controller = camera.GetComponent<HousePlacementCameraController>();
            Assert.IsNotNull(controller, "Furniture placement mode should attach a placement camera controller to the main camera.");
            Assert.IsNotNull(GameObject.Find("PlacementCameraFullViewButton")?.GetComponent<Button>(), "Player-facing full-view camera button should be available.");
        }

        [UnityTest]
        public IEnumerator FullViewButton_ZoomsOutAndKeepsCameraInsideBounds()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var camera = Camera.main;
            var beforeSize = camera.orthographicSize;
            GameObject.Find("PlacementCameraFullViewButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.GreaterOrEqual(camera.orthographicSize, beforeSize, "Full view should zoom out or keep the current wider framing.");
            Assert.LessOrEqual(Mathf.Abs(camera.transform.position.z + 10f), 0.1f, "Full view must preserve the 2D camera z position.");
        }

        [UnityTest]
        public IEnumerator ClosingPlacementPanel_RestoresCameraFollow()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var camera = Camera.main;
            var follow = camera.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow);
            var close = GameObject.Find("CloseFurniturePanelButton")?.GetComponent<Button>();
            Assert.IsNotNull(close);

            close.onClick.Invoke();
            yield return null;

            Assert.IsTrue(follow.enabled, "Closing furniture placement should restore player camera follow.");
        }
    }
}
```

- [ ] **Step 2: Run the PlayMode camera tests and confirm failure**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HousePlacementCameraControlPlayModeTests includeMessages=true
```

Expected before binding: missing `HousePlacementCameraController` and/or `PlacementCameraFullViewButton`.

- [ ] **Step 3: Bind controller in `InteriorFurniturePlacementPanel`**

Add a private field:

```csharp
private HousePlacementCameraController _cameraController;
```

Call after `BuildUi()` and before `RegenerateAndApply()` in `Create`:

```csharp
panel.BindPlacementCamera();
```

Add button to the toolbar:

```csharp
CreateActionButton(toolbar.transform, "PlacementCameraFullViewButton", "전체", new Vector2(405f, 0f), new Vector2(150f, 70f), FullViewCamera, 18);
```

Add methods:

```csharp
private void BindPlacementCamera()
{
    var camera = Camera.main;
    if (camera == null)
    {
        return;
    }

    _cameraController = camera.GetComponent<HousePlacementCameraController>();
    if (_cameraController == null)
    {
        _cameraController = camera.gameObject.AddComponent<HousePlacementCameraController>();
    }

    _cameraController.Configure(camera, ResolvePlacementBounds());
    _cameraController.BeginPlacementControl();
}

private Bounds ResolvePlacementBounds()
{
    var tilemap = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
    if (tilemap != null)
    {
        tilemap.CompressBounds();
        var cellBounds = tilemap.cellBounds;
        return new Bounds(cellBounds.center, cellBounds.size);
    }

    return new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));
}

private void FullViewCamera()
{
    if (_cameraController != null)
    {
        _cameraController.FullView();
    }
}
```

Update `OnDisable()`:

```csharp
private void OnDisable()
{
    _isClosing = true;
    if (_cameraController != null)
    {
        _cameraController.EndPlacementControl();
    }
}
```

- [ ] **Step 4: Run camera PlayMode tests green**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HousePlacementCameraControlPlayModeTests includeMessages=true
```

Expected: controller is attached, full-view button zooms out, close restores `CameraFollow`.

## Task 7: Required Automated Verification

**Files:**
- No new source files unless failures reveal missing coverage.

- [ ] **Step 1: Run focused EditMode tests**

Run:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Placement.FurnitureCatalogRegistryTests includeMessages=true
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.Interiors.PlacementCameraControllerTests includeMessages=true
```

Expected: all listed tests pass.

- [ ] **Step 2: Run focused PlayMode tests**

Run:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HouseInteriorFurniturePlacementPlayModeTests includeMessages=true
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.FurniturePlacementMouseFlowPlayModeTests includeMessages=true
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.Interiors.HousePlacementCameraControlPlayModeTests includeMessages=true
```

Expected: all listed tests pass.

- [ ] **Step 3: Run data-driven no-id-branching gate**

Run from the repo root:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
```

Expected: exit code 0. If it flags new runtime furniture id branching, remove that branch and move the behavior into ScriptableObject data or generic category/footprint logic.

## Task 8: Direct Visual Play Verification

**Files:**
- Update final report only unless a persistent audit artifact is requested.

- [ ] **Step 1: Enter House through PlayMode**

Use Unity MCP to open/load `Assets/Scenes/House.unity`, enter PlayMode, and wait for `InteriorFurniturePlacementPanel`.

Evidence to record:

- active scene: `House`
- panel object present: `InteriorFurniturePlacementPanel`
- camera object: `Main Camera`
- camera controller: `HousePlacementCameraController`

- [ ] **Step 2: Verify desk through actual UI**

Use the player-facing UI path:

1. Click `PaletteFurniture_desk.basic`.
2. Click a visible green placement cell.
3. Capture Game View or Camera screenshot.
4. Inspect runtime Tilemap state.

Evidence to record:

- selected furniture id: `desk.basic`
- clicked cell position
- object tilemap name: `HouseFurnitureObjectTilemap`
- placed tile names: one `ShadowlessOffice_Desk_Table_r39_c01`
- computer tile count: `0`
- occupied cells: one cell matching the desk cell

- [ ] **Step 3: Verify blackChair twice through actual UI**

Use the player-facing UI path:

1. Click `PaletteFurniture_chair.black`.
2. Click a visible green placement cell.
3. Click `PaletteFurniture_chair.black` again.
4. Click a second visible green placement cell.
5. Capture Game View or Camera screenshot.
6. Inspect runtime Tilemap state.

Evidence to record:

- selected furniture id: `chair.black`
- first and second clicked cell positions
- tile names: exactly two `BlackChair_00`
- `HousePlacementAvailabilityTilemap` valid/invalid counts after placement: `0/0`
- `HouseDecorationTilemap` tile at first chair cell: `null`
- `ModernOfficeChairSampleTilemap`: absent
- stale `FurniturePreview` or debug Tilemaps: absent or empty

- [ ] **Step 4: Verify placement camera controls**

Use the player-facing flow:

1. Record initial camera position and orthographic size.
2. Use mouse wheel or `+/-` to zoom.
3. Use `WASD` or arrow keys to pan.
4. Click `PlacementCameraFullViewButton`.
5. Capture Game View or Camera screenshot.
6. Close the panel and verify `CameraFollow.enabled == true`.

Evidence to record:

- initial and final camera orthographic size
- initial and final camera position
- camera bounds clamp result
- all placement-capable visible floor bounds are within view after full-view
- `CameraFollow` restored after panel close

- [ ] **Step 5: Verify saved scene/asset state if modified**

If any scene or asset was changed, inspect after saving:

- `Assets/Data/Interiors/Furniture/Furniture_Desk_Basic.asset` stable id, footprint, tile parts
- `Assets/Data/Registry/GameDataRegistry.asset` contains `Furniture_Desk_Basic`
- `Assets/Scenes/House.unity` has no visible `ModernOfficeChairSampleTilemap` or placeholder furniture object

Do not report completion if only runtime state was verified while saved data still differs.

## Task 9: Completion Audit

**Files:**
- Final response only, unless the user asks for a persistent audit document.

- [ ] **Step 1: Build prompt-to-artifact checklist**

Map each requirement from `docs/superpowers/goals/2026-05-17-house-furniture-placement-visual-bugfix-camera-goal.md` to evidence:

- desk 1x1: asset data, EditMode test, PlayMode UI test, screenshot/log
- no computer auto-attach: PlayMode tile count, screenshot/log
- blackChair alignment/cleanup: asset data, PlayMode double-placement test, screenshot/log
- camera zoom/pan/full-view: camera tests, screenshot/log
- `CameraFollow` restore: test and runtime inspection
- no id branching: CI gate output
- no Unity Console errors: console log inspection
- saved asset/scene state: asset/scene inspection if modified

- [ ] **Step 2: Report incomplete items honestly**

If direct Game View screenshot, PlayMode input path, saved state inspection, or CI gate is blocked, report the blocker explicitly and do not claim the goal is complete.

- [ ] **Step 3: Only then mark the active goal complete**

Call `update_goal` only after the audit shows every done criterion has concrete evidence.
