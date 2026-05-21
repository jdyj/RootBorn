# Boot Town House-Style Tiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Boot-to-Town player-facing view show saved, House-style tilemap content instead of runtime placeholder cue sprites.

**Architecture:** Treat `Assets/Scenes/Town.unity` as the source of truth for visible Town tile content. Keep `TownTilemapFallbackInstaller` as a narrow safety net for missing tilemap objects, and verify both saved scene state and the Boot-to-Town runtime view.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit Unity EditMode/PlayMode tests, Unity Tilemap, Unity MCP for `Assets/**/*.cs` edits, Unity Game View/Camera screenshot tools.

---

### Task 1: Add Saved Town Tilemap EditMode Coverage

**Files:**
- Create: `Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs`
- Inspect: `Assets/Scenes/Town.unity`

- [ ] **Step 1: Write the failing test**

Use the Unity MCP `script-update-or-create` flow for the new `.cs` file. Do not write `Assets/**/*.cs` directly from the filesystem.

```csharp
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownSavedTilemapSceneTests
    {
        [Test]
        public void TOWN_VISUAL_EDIT_001_TownSceneStoresVisibleTilemapLayout()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Single);

            try
            {
                Assert.AreEqual("Town", scene.name);
                AssertTilemapHasTiles("GroundTilemap", 600);
                AssertNoVisibleRuntimeCue("TownApartment");
                AssertNoVisibleRuntimeCue("TownStreet");
                AssertNoVisibleRuntimeCue("TownShop");
                AssertNoVisibleRuntimeCue("TownCommunityBoard");
            }
            finally
            {
                if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
                {
                    EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
                }
            }
        }

        private static void AssertTilemapHasTiles(string name, int minimumTiles)
        {
            Tilemap tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must exist in the saved Town scene.");
            Assert.GreaterOrEqual(CountTiles(tilemap), minimumTiles, name + " must contain saved visible tile data.");
        }

        private static void AssertNoVisibleRuntimeCue(string name)
        {
            GameObject cue = GameObject.Find(name);
            if (cue == null)
            {
                return;
            }

            SpriteRenderer renderer = cue.GetComponent<SpriteRenderer>();
            Assert.IsTrue(renderer == null || !renderer.enabled || !cue.activeInHierarchy, name + " must not be a visible saved placeholder cue in Town.unity.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails or exposes the current gap**

Run with Unity MCP:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownSavedTilemapSceneTests
```

Expected before implementation: failure if any visible runtime cue is saved, if the scene lacks the expected tile count, or if the test file has not compiled yet.

- [ ] **Step 3: Commit the red test if repository policy allows test-first commits**

```bash
git add Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs.meta
git commit -m "[TEST] Boot Town 저장 타일맵 검증 추가"
```

If Unity generated `.meta` files only after AssetDatabase refresh, include them in the same commit.

### Task 2: Add Boot-to-Town PlayMode Coverage

**Files:**
- Create: `Assets/Tests/PlayMode/TownBootTilemapVisualTests.cs`
- Inspect: `Assets/Scenes/Boot.unity`
- Inspect: `Assets/Scenes/Town.unity`

- [ ] **Step 1: Write the failing PlayMode test**

Use the Unity MCP `script-update-or-create` flow for the new `.cs` file.

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode
{
    public sealed class TownBootTilemapVisualTests
    {
        [UnityTest]
        public IEnumerator TOWN_VISUAL_PM_001_BootFlowShowsSavedTownTilemap()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);

            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != "Town" && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual("Town", SceneManager.GetActiveScene().name, "Boot flow must reach Town through the normal scene path.");

            Tilemap ground = GameObject.Find("GroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(ground, "Town runtime must expose the saved GroundTilemap.");
            Assert.GreaterOrEqual(CountTiles(ground), 600, "Boot-to-Town runtime must show the saved Town tile layout.");

            AssertNoVisibleRuntimeCue("TownApartment");
            AssertNoVisibleRuntimeCue("TownStreet");
            AssertNoVisibleRuntimeCue("TownShop");
            AssertNoVisibleRuntimeCue("TownCommunityBoard");
            Assert.IsNotNull(Camera.main, "Town must have a player-facing camera for visual verification.");
        }

        private static void AssertNoVisibleRuntimeCue(string name)
        {
            GameObject cue = GameObject.Find(name);
            if (cue == null)
            {
                return;
            }

            SpriteRenderer renderer = cue.GetComponent<SpriteRenderer>();
            Assert.IsTrue(renderer == null || !renderer.enabled || !cue.activeInHierarchy, name + " must not visibly cover the saved Town tilemap.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails or exposes the current gap**

Run with Unity MCP:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownBootTilemapVisualTests
```

Expected before implementation: failure if Boot does not reach Town in the test flow, if `GroundTilemap` is hidden or renamed unexpectedly, or if runtime cue sprites appear.

### Task 3: Narrow the Runtime Fallback

**Files:**
- Modify: `Assets/Scripts/Game/Bootstrap/TownTilemapFallbackInstaller.cs`
- Test: `Assets/Tests/PlayMode/TownBootTilemapVisualTests.cs`

- [ ] **Step 1: Change fallback behavior**

Use the Unity MCP `script-update-or-create` flow for `TownTilemapFallbackInstaller.cs`. Preserve the existing namespace and class name. Update `Ensure(Scene scene)` so it:

```csharp
private static void Ensure(Scene scene)
{
    var grid = FindRoot(scene, GridName);
    if (grid == null)
    {
        grid = new GameObject(GridName, typeof(Grid));
        SceneManager.MoveGameObjectToScene(grid, scene);
    }
    else if (grid.GetComponent<Grid>() == null)
    {
        grid.AddComponent<Grid>();
    }

    EnsureTilemap(grid.transform, "GroundTilemap", 0);
    EnsureTilemap(grid.transform, DecorationTilemapName, 10);
    DisableRuntimeCue(scene, "TownApartment");
    DisableRuntimeCue(scene, "TownStreet");
    DisableRuntimeCue(scene, "TownShop");
    DisableRuntimeCue(scene, "TownCommunityBoard");
}
```

Add this helper in the same class:

```csharp
private static void DisableRuntimeCue(Scene scene, string name)
{
    var go = FindRoot(scene, name);
    if (go == null)
    {
        return;
    }

    var renderer = go.GetComponent<SpriteRenderer>();
    if (renderer != null)
    {
        renderer.enabled = false;
    }
}
```

Remove calls to `EnsureTownCue` from `Ensure`. Leave `EnsureTownCue` and `CreateCueSprite` only if another compile path still needs them; otherwise remove the now-unused methods.

- [ ] **Step 2: Run focused tests**

Run with Unity MCP:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownBootTilemapVisualTests
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownSavedTilemapSceneTests
```

Expected after implementation: both tests pass, or any failure points to missing saved-scene content to fix in Task 4.

### Task 4: Complete Saved Town Scene Tile Layers

**Files:**
- Modify: `Assets/Scenes/Town.unity`
- Use existing asset: `Assets/Data/Tiles/GroundTile.asset`
- Optional existing asset: `Assets/Data/Interiors/RoomPresets/AutoImported/ModernInteriorsHomeDesigns/Condominium_Design/Tiles/*.asset`

- [ ] **Step 1: Inspect current saved Tilemap state**

Use Unity MCP scene inspection or script execution to report:

```text
Scene=Town
Tilemap=GroundTilemap
TileCount=<count>
Bounds=<bounds>
RendererEnabled=<true/false>
SortingOrder=<order>
VisibleCueSprites=<names>
```

Expected current baseline from repository inspection: `GroundTilemap` exists and contains roughly 725 saved tiles.

- [ ] **Step 2: Apply scene visual changes**

Use Unity Editor APIs through MCP, not ad hoc YAML editing, to:

- Keep `GroundTilemap` as the saved visible ground layer.
- Ensure `GroundTilemap` renderer is enabled and sorting order is `0`.
- Ensure `TownDecorationTilemap` exists under `Grid`, renderer enabled, sorting order `10`.
- Paint a small number of existing tile assets into `TownDecorationTilemap` only if the Game View needs visual accents after screenshot inspection.
- Disable or remove visible `TownApartment`, `TownStreet`, `TownShop`, and `TownCommunityBoard` cue sprite renderers if they exist in saved scene.
- Save `Assets/Scenes/Town.unity`.

- [ ] **Step 3: Reopen or re-inspect saved scene**

Use Unity MCP scene inspection after saving. Expected:

```text
GroundTilemap tile count >= 600
TownDecorationTilemap exists
Visible cue sprite renderers = 0
Scene path = Assets/Scenes/Town.unity
```

### Task 5: Player-Facing Visual Verification

**Files:**
- No source files expected.
- Inspect: runtime `Town` scene.

- [ ] **Step 1: Run Boot-to-Town PlayMode path**

Use Unity MCP to enter PlayMode from `Boot` or run the focused PlayMode test. Confirm:

```text
ActiveScene=Town
GroundTilemap tile count >= 600
TownDecorationTilemap exists
Visible cue sprite renderers = 0
Camera.main exists
```

- [ ] **Step 2: Capture player-facing screenshot**

Use one of:

```text
screenshot-game-view
screenshot-camera
```

Expected visual result: the first Town view shows tilemap ground content, not blank background or colored cue squares covering the layout.

- [ ] **Step 3: Final verification run**

Run with Unity MCP:

```text
tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownSavedTilemapSceneTests
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownBootTilemapVisualTests
```

Expected: both focused test classes pass with no unexpected console errors.

- [ ] **Step 4: Commit implementation**

```bash
git add Assets/Scripts/Game/Bootstrap/TownTilemapFallbackInstaller.cs Assets/Scripts/Game/Bootstrap/TownTilemapFallbackInstaller.cs.meta Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs Assets/Tests/EditMode/TownConcept/TownSavedTilemapSceneTests.cs.meta Assets/Tests/PlayMode/TownBootTilemapVisualTests.cs Assets/Tests/PlayMode/TownBootTilemapVisualTests.cs.meta Assets/Scenes/Town.unity
git commit -m "[UI][TEST] Boot Town 타일 화면 검증 추가"
```

Only include files touched by this plan. Do not stage unrelated existing workspace changes.

---

## Self-Review

Spec coverage:
- Saved `Town.unity` source of truth: Task 1 and Task 4.
- Runtime fallback narrowing: Task 3.
- Boot-to-Town player path: Task 2 and Task 5.
- Visual verification gate: Task 5.
- Avoid House system reuse: Task 4 uses only scene Tilemaps and existing Tile assets.

Placeholder scan:
- No `TBD`, `TODO`, or unspecified implementation steps remain.

Type consistency:
- Test class names and file names match the commands.
- Tilemap names are intentionally `GroundTilemap` for saved Town ground and `TownDecorationTilemap` for fallback decoration.
