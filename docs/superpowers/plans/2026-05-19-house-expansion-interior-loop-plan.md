# House Expansion Interior Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a player-facing House expansion loop where stage 1 expansion drives room preset application, furniture placement, save/load restore, and QA simulation evidence.

**Architecture:** Keep saved House state as the source of truth, extend it with the selected room preset id, and let House load bind generation, preset, placement surface, and furniture restore in that order. Use existing `Rootborn.Game.Housing`, `Rootborn.Game.Interiors`, `Rootborn.Game.Placement`, and `Rootborn.UI.Interiors` boundaries; add only small adapters and probes where the boundary between systems is currently implicit.

**Tech Stack:** Unity 6000.3, C#, ScriptableObject data, Unity Tilemaps, PlayMode/EditMode NUnit tests, Unity MCP `script-update-or-create` for every `Assets/**/*.cs` change, PowerShell QA runner for repeatable simulation.

---

## File Structure

- Modify `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`: add `SelectedRoomPresetId` to `HouseStateSaveData` and normalize it.
- Modify `Assets/Scripts/Game/Interiors/InteriorRoomPresetRuntime.cs`: add fit validation and save/load helpers for the selected preset id.
- Modify `Assets/Scripts/UI/Interiors/InteriorRoomPresetToolbarInstaller.cs`: save the selected preset id and refresh placement surface after applying a preset.
- Modify `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`: expose probe data for active surface bounds, placed furniture, overlay visibility, and restore skip count.
- Modify `Assets/Scripts/Game/Placement/FurniturePlacementLayoutPersistence.cs`: add stage-scoped save/load overloads while preserving the current default House layout file.
- Create `Assets/Scripts/Game/Interiors/HouseInteriorLoopProbe.cs`: runtime probe for stage, preset, tile counts, surface bounds, furniture cells, and overlay cleanup state.
- Modify `Assets/Tests/EditMode/Interiors/HomeDesignRoomPresetTests.cs`: cover preset fit and non-destructive preset application.
- Modify `Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs`: cover stage-scoped furniture layout restore and invalid entry skip.
- Create `Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs`: integrated House expansion, preset, furniture placement, reload, and overlay cleanup scenarios.
- Create `Scripts/qa/run-house-expansion-interior-loop-simulation.ps1`: repeatable QA runner for Unity PlayMode simulation plus evidence collection.
- Create `docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md`: verification command, result, logs, screenshots, probes.

## Required Constraints

- Do not edit `Assets/**/*.cs` with shell redirection, `apply_patch`, or direct file writes. Use Unity MCP `script-update-or-create`.
- Do not add `if (presetId == "...")`, `switch(stageId)`, or furniture-specific C# branches.
- Do not claim completion for visible work without PlayMode path verification plus screenshot/probe evidence.
- Keep commits small:
  - `[FEATURE][TEST] House 인테리어 저장 상태 확장`
  - `[FEATURE][TEST] House 프리셋과 배치 표면 연결`
  - `[FEATURE][TEST] House 가구 배치 reload 시뮬레이션 추가`
  - `[DOCS][TEST] House 인테리어 루프 검증 기록`

---

### Task 1: Persist Selected Room Preset In House State

**Files:**
- Modify: `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`
- Test: `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`

- [ ] **Step 1: Add failing EditMode test**

Use `script-update-or-create` to append this test to `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`:

```csharp
[Test]
public void HOUSE_STATE_001_SelectedRoomPresetIdPersistsThroughNormalize()
{
    var state = new HouseStateSaveData
    {
        CurrentStageIndex = 1,
        SelectedRoomPresetId = "preset.expanded.study"
    };

    HouseStatePersistence.Save("house-state-preset-test", state);
    var loaded = HouseStatePersistence.Load("house-state-preset-test");

    Assert.AreEqual(1, loaded.CurrentStageIndex);
    Assert.AreEqual("preset.expanded.study", loaded.SelectedRoomPresetId);
}
```

- [ ] **Step 2: Run the failing test**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Housing","testClass":"HouseUpgradeDefinitionTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: compile failure because `HouseStateSaveData.SelectedRoomPresetId` is not defined.

- [ ] **Step 3: Add the saved field**

Use `script-update-or-create` on `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`.

Add this field to `HouseStateSaveData`:

```csharp
public string SelectedRoomPresetId = string.Empty;
```

Update `Normalize`:

```csharp
state.SelectedRoomPresetId ??= string.Empty;
```

- [ ] **Step 4: Run the test to verify pass**

Run the same `tests-run` command from Step 2.

Expected: `HOUSE_STATE_001_SelectedRoomPresetIdPersistsThroughNormalize` passes.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Housing/HouseStatePersistence.cs Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs
git commit -m "[FEATURE][TEST] House 인테리어 프리셋 저장 상태 추가"
```

---

### Task 2: Add Preset Fit Validation And Save Helper

**Files:**
- Modify: `Assets/Scripts/Game/Interiors/InteriorRoomPresetRuntime.cs`
- Test: `Assets/Tests/EditMode/Interiors/HomeDesignRoomPresetTests.cs`

- [ ] **Step 1: Add failing EditMode tests**

Use `script-update-or-create` to add these tests to `Assets/Tests/EditMode/Interiors/HomeDesignRoomPresetTests.cs`:

```csharp
[Test]
public void HOUSE_PRESET_001_FitValidationRejectsPresetOutsideGeneratedBounds()
{
    var preset = InteriorRoomPresetDefinition.CreateForTests("preset.too-large", "Too Large", new Vector2Int(40, 40), Array.Empty<InteriorRoomPresetTileCell>());

    var map = InteriorGenerator.Generate(InteriorGenerationProfile.CreateDefaultOfficeForTests(), 1205);

    Assert.IsFalse(InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, map));
}

[Test]
public void HOUSE_PRESET_002_FitValidationAcceptsPresetInsideGeneratedBounds()
{
    var preset = InteriorRoomPresetDefinition.CreateForTests("preset.fits", "Fits", new Vector2Int(6, 6), Array.Empty<InteriorRoomPresetTileCell>());

    var map = InteriorGenerator.Generate(InteriorGenerationProfile.CreateDefaultOfficeForTests(), 1205);

    Assert.IsTrue(InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, map));
}
```

- [ ] **Step 2: Run the tests to verify failure**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Interiors","testClass":"HomeDesignRoomPresetTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: compile failure because `InteriorRoomPresetRuntimeUtility` does not exist.

- [ ] **Step 3: Implement utility**

Use `script-update-or-create` on `Assets/Scripts/Game/Interiors/InteriorRoomPresetRuntime.cs`.

Add this class in namespace `Rootborn.Game.Interiors`:

```csharp
public static class InteriorRoomPresetRuntimeUtility
{
    public static bool CanFitPreset(InteriorRoomPresetDefinition preset, InteriorGeneratedMap map)
    {
        if (preset == null || map == null)
        {
            return false;
        }

        return preset.Size.x > 0 &&
               preset.Size.y > 0 &&
               preset.Size.x <= map.Width &&
               preset.Size.y <= map.Height;
    }

    public static void SaveSelectedPreset(string saveSlot, string presetId)
    {
        var state = Rootborn.Game.Housing.HouseStatePersistence.Load(saveSlot);
        state.SelectedRoomPresetId = presetId ?? string.Empty;
        Rootborn.Game.Housing.HouseStatePersistence.Save(saveSlot, state);
    }
}
```

- [ ] **Step 4: Run tests**

Run the same `HomeDesignRoomPresetTests` command.

Expected: both `HOUSE_PRESET_001` and `HOUSE_PRESET_002` pass.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Interiors/InteriorRoomPresetRuntime.cs Assets/Tests/EditMode/Interiors/HomeDesignRoomPresetTests.cs
git commit -m "[FEATURE][TEST] House 방 프리셋 크기 검증 추가"
```

---

### Task 3: Persist Preset Selection Through Toolbar

**Files:**
- Modify: `Assets/Scripts/UI/Interiors/InteriorRoomPresetToolbarInstaller.cs`
- Modify: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- Modify: `Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs`
- Test: `Assets/Tests/PlayMode/Interiors/HomeDesignRoomPresetPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode test**

Use `script-update-or-create` to add this test to `Assets/Tests/PlayMode/Interiors/HomeDesignRoomPresetPlayModeTests.cs`:

```csharp
[UnityTest]
public IEnumerator HOUSE_LOOP_PM_002_RoomPresetAppliesToExpandedHouseWithoutClearingFurniture()
{
    const string saveSlot = "house-loop-preset-playmode";
    ActiveSaveContext.SetSlotForTests(saveSlot);

    var state = new HouseStateSaveData { CurrentStageIndex = 1 };
    HouseStatePersistence.Save(saveSlot, state);

    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var applier = Object.FindFirstObjectByType<InteriorTilemapApplier>();
    Assert.IsNotNull(applier);

    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
    Assert.IsNotNull(overlay);
    Assert.Greater(overlay.ActiveSurfaceCellCountForTests, 0);

    var preset = InteriorRoomPresetCatalog.LoadPresets().FirstOrDefault();
    Assert.IsNotNull(preset, "At least one imported room preset is required for the House loop.");

    Assert.IsTrue(InteriorRoomPresetToolbarInstaller.ApplyPresetForTests(preset));

    var loaded = HouseStatePersistence.Load(saveSlot);
    Assert.AreEqual(preset.StableId, loaded.SelectedRoomPresetId);
    Assert.Greater(overlay.ActiveSurfaceCellCountForTests, 0);
}
```

- [ ] **Step 2: Run the failing test**

Run:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Interiors","testClass":"HomeDesignRoomPresetPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: compile failure for `ActiveSurfaceCellCountForTests` or `ApplyPresetForTests`.

- [ ] **Step 3: Expose active surface probe**

Use `script-update-or-create` on `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`.

Add property:

```csharp
public int ActiveSurfaceCellCountForTests => _surface != null ? _surface.Bounds.size.x * _surface.Bounds.size.y : 0;
```

- [ ] **Step 4: Expose last generated map**

Use `script-update-or-create` on `Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs`.

Add field:

```csharp
private InteriorGeneratedMap _lastGeneratedMap;
```

Add property:

```csharp
public InteriorGeneratedMap LastGeneratedMap => _lastGeneratedMap;
```

At the start of `Apply(InteriorGeneratedMap map)`, after the null guard, assign:

```csharp
_lastGeneratedMap = map;
```

- [ ] **Step 5: Add test-facing toolbar apply method**

Use `script-update-or-create` on `Assets/Scripts/UI/Interiors/InteriorRoomPresetToolbarInstaller.cs`.

Add method:

```csharp
public static bool ApplyPresetForTests(InteriorRoomPresetDefinition preset)
{
    return ApplyPresetAndPersist(preset);
}
```

Refactor existing preset click handler to call:

```csharp
private static bool ApplyPresetAndPersist(InteriorRoomPresetDefinition preset)
{
    if (preset == null)
    {
        return false;
    }

    var applier = FindFirstObjectByType<InteriorTilemapApplier>();
    if (applier == null || applier.LastGeneratedMap == null)
    {
        return false;
    }

    if (!InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, applier.LastGeneratedMap))
    {
        return false;
    }

    var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
    if (floor == null)
    {
        return false;
    }

    InteriorRoomPresetApplier.ApplyBaseLayer(preset, floor);
    InteriorRoomPresetRuntimeProbe.Record(preset, floor);
    InteriorRoomPresetRuntimeUtility.SaveSelectedPreset(ActiveSaveContext.SlotId, preset.StableId);

    var overlay = InteriorPlacementPreviewOverlay.Ensure();
    overlay?.BindGeneratedMapForPersistence(applier.LastGeneratedMap, applier);
    return true;
}
```

- [ ] **Step 6: Run tests**

Run the `HomeDesignRoomPresetPlayModeTests` command.

Expected: `HOUSE_LOOP_PM_002` passes.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/UI/Interiors/InteriorRoomPresetToolbarInstaller.cs Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs Assets/Tests/PlayMode/Interiors/HomeDesignRoomPresetPlayModeTests.cs
git commit -m "[FEATURE][TEST] House 프리셋 선택 저장 연결"
```

---

### Task 4: Stage-Scoped Furniture Layout Persistence

**Files:**
- Modify: `Assets/Scripts/Game/Placement/FurniturePlacementLayoutPersistence.cs`
- Modify: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- Test: `Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs`

- [ ] **Step 1: Add failing EditMode test**

Use `script-update-or-create` to add this test to `Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs`:

```csharp
[Test]
public void HOUSE_FURNITURE_SAVE_001_StageScopedLayoutsDoNotOverwriteEachOther()
{
    ActiveSaveContext.SetSlotForTests("house-furniture-stage-scope");

    var stage0 = new[]
    {
        new FurniturePlacementSaveData
        {
            FurnitureId = "chair",
            AnchorX = 0,
            AnchorY = 0,
            Direction = FurniturePlacementDirection.North
        }
    };

    var stage1 = new[]
    {
        new FurniturePlacementSaveData
        {
            FurnitureId = "desk",
            AnchorX = 2,
            AnchorY = 2,
            Direction = FurniturePlacementDirection.East
        }
    };

    FurniturePlacementLayoutPersistence.SaveHouseLayoutForStage(0, stage0);
    FurniturePlacementLayoutPersistence.SaveHouseLayoutForStage(1, stage1);

    var loaded = new List<FurniturePlacementSaveData>();
    Assert.IsTrue(FurniturePlacementLayoutPersistence.TryLoadHouseLayoutForStage(1, loaded));
    Assert.AreEqual(1, loaded.Count);
    Assert.AreEqual("desk", loaded[0].FurnitureId);
}
```

- [ ] **Step 2: Run failing test**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Placement","testClass":"FurniturePlacementSaveTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: compile failure for stage-scoped methods.

- [ ] **Step 3: Implement stage-scoped methods**

Use `script-update-or-create` on `Assets/Scripts/Game/Placement/FurniturePlacementLayoutPersistence.cs`.

Add:

```csharp
public static string GetHouseLayoutFileNameForStage(int stageIndex)
{
    return "house-furniture-layout-stage-" + Mathf.Max(0, stageIndex) + ".json";
}

public static void SaveHouseLayoutForStage(int stageIndex, IReadOnlyList<FurniturePlacementSaveData> items)
{
    SaveLayout(GetHouseLayoutFileNameForStage(stageIndex), items);
}

public static bool TryLoadHouseLayoutForStage(int stageIndex, List<FurniturePlacementSaveData> destination)
{
    return TryLoadLayout(GetHouseLayoutFileNameForStage(stageIndex), destination);
}
```

Refactor existing `SaveHouseLayout`:

```csharp
public static void SaveHouseLayout(IReadOnlyList<FurniturePlacementSaveData> items)
{
    SaveLayout(HouseLayoutFileName, items);
}
```

Refactor existing `TryLoadHouseLayout`:

```csharp
public static bool TryLoadHouseLayout(List<FurniturePlacementSaveData> destination)
{
    return TryLoadLayout(HouseLayoutFileName, destination);
}
```

Add private helpers:

```csharp
private static void SaveLayout(string fileName, IReadOnlyList<FurniturePlacementSaveData> items)
{
    var layout = new FurniturePlacementLayoutSaveData { Items = ToArray(items) };
    CreateService().WriteJson(fileName, JsonUtility.ToJson(layout, true));
}

private static bool TryLoadLayout(string fileName, List<FurniturePlacementSaveData> destination)
{
    if (destination == null)
    {
        return false;
    }

    string json = CreateService().ReadJson(fileName);
    if (string.IsNullOrEmpty(json))
    {
        return false;
    }

    try
    {
        var layout = JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(json);
        destination.Clear();
        if (layout == null || layout.Items == null)
        {
            return false;
        }

        for (int i = 0; i < layout.Items.Length; i++)
        {
            if (layout.Items[i] != null)
            {
                destination.Add(layout.Items[i]);
            }
        }

        return destination.Count > 0;
    }
    catch (ArgumentException)
    {
        return false;
    }
}
```

- [ ] **Step 4: Save and load by active House stage in overlay**

Use `script-update-or-create` on `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`.

In `SaveFurnitureLayout`, replace:

```csharp
FurniturePlacementLayoutPersistence.SaveHouseLayout(SavedFurnitureLayout);
```

with:

```csharp
int stageIndex = Rootborn.Game.Housing.HouseStatePersistence.Load(Rootborn.Game.Save.ActiveSaveContext.SlotId).CurrentStageIndex;
FurniturePlacementLayoutPersistence.SaveHouseLayoutForStage(stageIndex, SavedFurnitureLayout);
```

In restore/load code that calls `TryLoadHouseLayout`, use:

```csharp
int stageIndex = Rootborn.Game.Housing.HouseStatePersistence.Load(Rootborn.Game.Save.ActiveSaveContext.SlotId).CurrentStageIndex;
FurniturePlacementLayoutPersistence.TryLoadHouseLayoutForStage(stageIndex, SavedFurnitureLayout);
```

- [ ] **Step 5: Run tests**

Run the `FurniturePlacementSaveTests` command.

Expected: `HOUSE_FURNITURE_SAVE_001` passes.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Placement/FurniturePlacementLayoutPersistence.cs Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs
git commit -m "[FEATURE][TEST] House 가구 배치 저장을 단계별로 분리"
```

---

### Task 5: Add Runtime Probe For Visual Simulation

**Files:**
- Create: `Assets/Scripts/Game/Interiors/HouseInteriorLoopProbe.cs`
- Test: `Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode test**

Use `script-update-or-create` to create `Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs` with:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseExpansionInteriorLoopPlayModeTests
    {
        [UnityTest]
        public IEnumerator HOUSE_LOOP_PM_001_HireExpansionUpdatesHouseBoundsAndPlacementSurface()
        {
            const string saveSlot = "house-loop-stage-bounds";
            ActiveSaveContext.SetSlotForTests(saveSlot);
            HouseStatePersistence.Save(saveSlot, new HouseStateSaveData { CurrentStageIndex = 1 });

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var probe = HouseInteriorLoopProbe.Capture();

            Assert.AreEqual(1, probe.CurrentStageIndex);
            Assert.Greater(probe.GroundTileCount, 0);
            Assert.Greater(probe.SurfaceCellCount, 0);
            Assert.IsFalse(probe.HasVisibleDebugOverlay);
        }
    }
}
```

- [ ] **Step 2: Run failing test**

Run:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Interiors","testClass":"HouseExpansionInteriorLoopPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: compile failure because `HouseInteriorLoopProbe` does not exist.

- [ ] **Step 3: Implement probe**

Use `script-update-or-create` to create `Assets/Scripts/Game/Interiors/HouseInteriorLoopProbe.cs`:

```csharp
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using Rootborn.Game.Placement;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    public sealed class HouseInteriorLoopProbe
    {
        public int CurrentStageIndex;
        public string SelectedRoomPresetId;
        public int GroundTileCount;
        public int WallTileCount;
        public int CollisionTileCount;
        public int SurfaceCellCount;
        public int PlacedFurnitureCount;
        public bool HasVisibleDebugOverlay;

        public static HouseInteriorLoopProbe Capture()
        {
            string slot = !string.IsNullOrEmpty(ActiveSaveContext.SlotId) ? ActiveSaveContext.SlotId : "default";
            var state = HouseStatePersistence.Load(slot);
            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);

            return new HouseInteriorLoopProbe
            {
                CurrentStageIndex = state.CurrentStageIndex,
                SelectedRoomPresetId = state.SelectedRoomPresetId,
                GroundTileCount = CountTiles("HouseGroundTilemap"),
                WallTileCount = CountTiles("HouseWallTilemap"),
                CollisionTileCount = CountTiles("HouseCollisionTilemap"),
                SurfaceCellCount = overlay != null ? overlay.ActiveSurfaceCellCountForTests : 0,
                PlacedFurnitureCount = overlay != null ? overlay.PlacedFurnitureCountForTests : 0,
                HasVisibleDebugOverlay = HasVisibleOverlay()
            };
        }

        private static int CountTiles(string name)
        {
            var tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasVisibleOverlay()
        {
            var tilemaps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                string name = tilemaps[i].gameObject.name;
                if (tilemaps[i].enabled && (name.Contains("Debug") || name.Contains("Sample") || name.Contains("GhostPreview") || name.Contains("FootprintPreview")))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Expose placed furniture count**

Use `script-update-or-create` on `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`.

Add:

```csharp
public int PlacedFurnitureCountForTests => _registry.Instances.Count;
```

- [ ] **Step 5: Run PlayMode test**

Run the `HouseExpansionInteriorLoopPlayModeTests` command.

Expected: `HOUSE_LOOP_PM_001` passes.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Interiors/HouseInteriorLoopProbe.cs Assets/Scripts/Game/Interiors/HouseInteriorLoopProbe.cs.meta Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs.meta
git commit -m "[FEATURE][TEST] House 인테리어 루프 런타임 프로브 추가"
```

---

### Task 6: Integrated Furniture Place Rotate Move Reload Test

**Files:**
- Modify: `Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs`
- Modify: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`

- [ ] **Step 1: Add failing PlayMode test**

Use `script-update-or-create` to add:

```csharp
[UnityTest]
public IEnumerator HOUSE_LOOP_PM_003_FurniturePlaceRotateMovePersistsAfterReload()
{
    const string saveSlot = "house-loop-furniture-reload";
    ActiveSaveContext.SetSlotForTests(saveSlot);
    HouseStatePersistence.Save(saveSlot, new HouseStateSaveData { CurrentStageIndex = 1 });

    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
    Assert.IsNotNull(overlay);

    overlay.RotateActiveFurniture();
    Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var placeMessage), placeMessage);
    Assert.IsTrue(overlay.MoveSelectedFurniture());
    Assert.IsTrue(overlay.SaveFurnitureLayout());

    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var probe = HouseInteriorLoopProbe.Capture();
    Assert.AreEqual(1, probe.CurrentStageIndex);
    Assert.Greater(probe.PlacedFurnitureCount, 0);
    Assert.IsFalse(probe.HasVisibleDebugOverlay);
}
```

- [ ] **Step 2: Run failing test**

Run the `HouseExpansionInteriorLoopPlayModeTests` command from Task 5.

Expected: test fails if no active furniture is selected by the House placement UI in the scene.

- [ ] **Step 3: Add deterministic test furniture selector**

Use `script-update-or-create` on `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`.

Add:

```csharp
public bool SelectFirstFurnitureForTests()
{
    var catalog = InteriorFurnitureCatalog.LoadFurniture();
    for (int i = 0; i < catalog.Count; i++)
    {
        if (catalog[i] != null && catalog[i].PlacementDefinition != null)
        {
            RefreshFurniture(_map, _request, _applier, catalog[i]);
            return true;
        }
    }

    LastMessage = "No furniture definition available";
    return false;
}
```

Update the test before rotation:

```csharp
Assert.IsTrue(overlay.SelectFirstFurnitureForTests(), overlay.LastMessage);
```

- [ ] **Step 4: Run PlayMode test**

Run the `HouseExpansionInteriorLoopPlayModeTests` command.

Expected: `HOUSE_LOOP_PM_003` passes.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs Assets/Tests/PlayMode/Interiors/HouseExpansionInteriorLoopPlayModeTests.cs
git commit -m "[FEATURE][TEST] House 가구 배치 reload 루프 검증 추가"
```

---

### Task 7: Add Repeatable QA Simulation Runner

**Files:**
- Create: `Scripts/qa/run-house-expansion-interior-loop-simulation.ps1`
- Modify: `docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md`

- [ ] **Step 1: Create simulation runner**

Use `apply_patch` to create `Scripts/qa/run-house-expansion-interior-loop-simulation.ps1`:

```powershell
param(
    [string]$RunName = "",
    [string]$SaveSlot = "",
    [string]$EvidenceDir = "",
    [int]$ReloadCount = 1
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "house-expansion-loop-" + (Get-Date -Format "yyyyMMdd-HHmmss")
    }

    if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
        $SaveSlot = $RunName
    }

    if ([string]::IsNullOrWhiteSpace($EvidenceDir)) {
        $EvidenceDir = Join-Path "production\qa\evidence" $RunName
    }

    New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null

    $json = @{
        testMode = "PlayMode"
        testNamespace = "Rootborn.Tests.PlayMode.Interiors"
        testClass = "HouseExpansionInteriorLoopPlayModeTests"
        includePassingTests = $true
        includeMessages = $true
        includeStacktrace = $true
        includeLogs = $true
        logType = "Error"
    } | ConvertTo-Json -Compress

    $inputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
    [System.IO.File]::WriteAllText($inputPath, $json, [System.Text.UTF8Encoding]::new($false))
    try {
        unity-mcp-cli.cmd run-tool tests-run --input-file $inputPath | Tee-Object -FilePath (Join-Path $EvidenceDir "playmode-results.txt")
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) {
            Remove-Item -LiteralPath $inputPath -Force
        }
    }

    $probe = @{
        RunName = $RunName
        SaveSlot = $SaveSlot
        ReloadCount = $ReloadCount
        EvidenceDir = (Resolve-Path $EvidenceDir).Path
        RequiredScreenshots = @(
            "after-expansion.png",
            "after-placement.png",
            "after-reload.png"
        )
    } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path $EvidenceDir "simulation-probe.json"), $probe, [System.Text.UTF8Encoding]::new($false))

    [PSCustomObject]@{
        Result = "Passed"
        RunName = $RunName
        SaveSlot = $SaveSlot
        EvidenceDir = (Resolve-Path $EvidenceDir).Path
    } | Format-List
}
finally {
    Pop-Location
}
```

- [ ] **Step 2: Run simulation**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-house-expansion-interior-loop-simulation.ps1 -RunName house-expansion-loop-verify-20260519 -SaveSlot house-expansion-loop-verify-20260519
```

Expected: `Result : Passed`.

- [ ] **Step 3: Record audit**

Use `apply_patch` to create `docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md`:

```markdown
# House Expansion Interior Loop Verification

Date: 2026-05-19

## Scope

Verified House expansion stage 1, room preset persistence, furniture placement persistence, reload restore, and overlay cleanup.

## Commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-house-expansion-interior-loop-simulation.ps1 -RunName house-expansion-loop-verify-20260519 -SaveSlot house-expansion-loop-verify-20260519
```

## Result

Result: Passed

## Evidence

- PlayMode result log: `production/qa/evidence/house-expansion-loop-verify-20260519/playmode-results.txt`
- Probe: `production/qa/evidence/house-expansion-loop-verify-20260519/simulation-probe.json`
- Screenshot after expansion: recorded by visual verification step
- Screenshot after placement: recorded by visual verification step
- Screenshot after reload: recorded by visual verification step

## Notes

The simulation runner verifies the automated path. Direct Game View screenshot capture must be attached before final completion if the runner does not produce image files in the current environment.
```

- [ ] **Step 4: Commit**

```powershell
git add -- Scripts/qa/run-house-expansion-interior-loop-simulation.ps1 docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md
git commit -m "[DOCS][TEST] House 인테리어 루프 시뮬레이션 추가"
```

---

### Task 8: Direct Visual Verification And Final Gate

**Files:**
- Modify: `docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md`

- [ ] **Step 1: Run focused EditMode tests**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Interiors","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: no failing tests.

- [ ] **Step 2: Run focused PlayMode tests**

Run:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Interiors","testClass":"HouseExpansionInteriorLoopPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: all `HOUSE_LOOP_PM_*` tests pass.

- [ ] **Step 3: Capture player-facing screenshots**

Use Unity MCP screenshot tools after entering PlayMode through the House flow:

```text
screenshot_game_view
```

Save or record evidence paths for:

- after stage 1 expansion;
- after preset application;
- after furniture placement;
- after House reload.

- [ ] **Step 4: Inspect runtime state**

Use Unity MCP object/component inspection or the probe test output to verify:

```text
CurrentStageIndex = 1
SelectedRoomPresetId is not empty
GroundTileCount > 0
WallTileCount > 0
SurfaceCellCount > 0
PlacedFurnitureCount > 0
HasVisibleDebugOverlay = false
```

- [ ] **Step 5: Update audit**

Append exact commands, pass/fail result, screenshot paths, and probe values to `docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md`.

- [ ] **Step 6: Commit and push**

```powershell
git add -- docs/superpowers/audits/2026-05-19-house-expansion-interior-loop-verification.md
git commit -m "[DOCS][TEST] House 인테리어 루프 검증 결과 기록"
git push origin town-playable-baseline-recovery
```

Expected: remote branch contains all implementation and verification commits.

---

## Self-Review

Spec coverage:

- Stage source of truth: Task 1 and Task 5.
- Room preset persistence and fit validation: Task 2 and Task 3.
- Furniture layout stage scoping and reload restore: Task 4 and Task 6.
- Test simulation: Task 7.
- Direct visual verification: Task 8.
- Performance constraints: implemented through bounded probe/restore checks and event-triggered refresh points; no new polling loops are planned.

Placeholder scan:

- No `TBD`, incomplete placeholder sections, or unspecified test commands are intentionally present.

Type consistency:

- `HouseStateSaveData.SelectedRoomPresetId` is introduced before usage.
- `InteriorRoomPresetRuntimeUtility` is introduced before toolbar usage.
- `ActiveSurfaceCellCountForTests`, `PlacedFurnitureCountForTests`, and `SelectFirstFurnitureForTests` are introduced before PlayMode test reliance.
