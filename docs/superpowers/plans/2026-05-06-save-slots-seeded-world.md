# Save Slots Seeded World Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three UI save slots with slot-scoped character/seed metadata, then generate Farm tiles and natural props deterministically from ScriptableObject world generation rules.

**Architecture:** Keep the existing `ModeSelectPanel -> CharacterCreator -> FarmAutoFiller` flow, but insert `SaveSlotSelectPanel` between menu and character creation. Save metadata lives under `Application.persistentDataPath/saves/<slot>/metadata.json`; world generation is split into pure deterministic generation (`SeededWorldGenerator`) and Unity scene application (`FarmAutoFiller`).

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject data, Unity Tilemap, Unity Test Framework EditMode/PlayMode, Unity MCP `script-update-or-create`, `assets-*`, `tests-run`

---

## File Structure

### Create With Unity MCP `script-update-or-create`

- `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`
  - JSON DTOs: `SaveSlotMetadata`, `SaveSlotSummary`, `ActiveSaveContext`
- `Assets/Scripts/Game/WorldGeneration/WeightedTileVariant.cs`
  - serializable weighted tile entry
- `Assets/Scripts/Game/WorldGeneration/TileVariantSetDefinition.cs`
  - SO tile candidate set and deterministic weighted picker
- `Assets/Scripts/Game/WorldGeneration/TilePatternDefinition.cs`
  - SO pattern grid that maps world cells to variant sets
- `Assets/Scripts/Game/WorldGeneration/TerrainReservedArea.cs`
  - serializable rectangle/safe-radius reserved area
- `Assets/Scripts/Game/WorldGeneration/NaturalPropSpawnDefinition.cs`
  - SO-backed resource spawn rule
- `Assets/Scripts/Game/WorldGeneration/TerrainGenerationDefinition.cs`
  - SO root generation definition
- `Assets/Scripts/Game/WorldGeneration/GeneratedWorld.cs`
  - generated tile/prop DTOs
- `Assets/Scripts/Game/WorldGeneration/SeededWorldGenerator.cs`
  - pure deterministic generator
- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
  - runtime-created 3-slot UI
- `Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs`
  - save slot service coverage
- `Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs`
  - deterministic generation and rule coverage
- `Assets/Tests/PlayMode/SaveSlotUiFlowTests.cs`
  - basic slot UI display coverage

### Modify With Unity MCP `script-update-or-create`

- `Assets/Scripts/Game/Save/SaveService.cs`
  - add slot list/create/delete/metadata APIs
- `Assets/Scripts/Game/Player/CharacterCustomization.cs`
  - keep legacy `Load()`/`Save()` compatibility and add `CopyFrom(CharacterCustomization other)` for slot metadata transfer
- `Assets/Scripts/UI/CharacterCreator.cs`
  - add `ShowForSlot(SaveSlotMetadata pendingMetadata)` and save metadata on start
- `Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs`
  - show `SaveSlotSelectPanel` on single-player start
- `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
  - read active save metadata, call `SeededWorldGenerator`, apply generated tiles/props
- `Assets/Scripts/Game/Common/GameDataRegistry.cs`
  - add `TerrainGenerationDefinition DefaultFarmTerrainGeneration`
- `Assets/Scripts/Editor/Tools/GenerateDefaultData.cs`
  - create default generation SO assets and register them

### Create Or Modify Assets With Unity MCP

- `Assets/Data/WorldGeneration/Terrain_Farm_Default.asset`
- `Assets/Data/WorldGeneration/Pattern_Grass_Default.asset`
- `Assets/Data/WorldGeneration/TileVariants_Grass_Default.asset`
- `Assets/Data/WorldGeneration/Spawn_Trees_Default.asset`
- `Assets/Data/WorldGeneration/Spawn_Rocks_Default.asset`
- `Assets/Data/Registry/GameDataRegistry.asset`

## Task 1: Save Slot Metadata Tests

**Files:**

- Create: `Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs`
- Modify: `Assets/Scripts/Game/Save/SaveService.cs`
- Create: `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`

- [ ] **Step 1: Write the failing test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Save
{
    public sealed class SaveSlotServiceTests
    {
        [Test]
        public void ListUiSlots_ReturnsThreeEmptySlots()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var slots = service.ListUiSlots();
                Assert.AreEqual(3, slots.Count);
                Assert.AreEqual("slot-0", slots[0].SlotId);
                Assert.AreEqual("slot-1", slots[1].SlotId);
                Assert.AreEqual("slot-2", slots[2].SlotId);
                Assert.IsFalse(slots[0].Exists);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void SaveAndLoadMetadata_PreservesSeedsAndCharacter()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                var character = new CharacterCustomization { BodyIndex = 1, EyesIndex = 2, HairstyleIndex = 3, OutfitIndex = 4, AccessoryIndex = 5 };
                var metadata = service.CreateMetadata("slot-0", character, 1234, 5678);
                service.SaveMetadata(metadata);

                var loaded = service.LoadMetadata("slot-0");
                Assert.IsNotNull(loaded);
                Assert.AreEqual("slot-0", loaded.SlotId);
                Assert.AreEqual(1234, loaded.WorldSeed);
                Assert.AreEqual(5678, loaded.TileSeed);
                Assert.AreEqual(1, loaded.Character.BodyIndex);
                Assert.AreEqual(5, loaded.Character.AccessoryIndex);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void DeleteSlot_RemovesMetadata()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                service.SaveMetadata(service.CreateMetadata("slot-1", new CharacterCustomization(), 11, 22));
                Assert.IsTrue(service.DeleteSlot("slot-1"));
                Assert.IsNull(service.LoadMetadata("slot-1"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void CreateMetadata_RejectsUnsafeSlotId()
        {
            var root = MakeTempRoot();
            try
            {
                var service = new SaveService("slot-0", root);
                Assert.Throws<System.ArgumentException>(() => service.CreateMetadata("../bad", new CharacterCustomization(), 1, 2));
            }
            finally { Directory.Delete(root, true); }
        }

        private static string MakeTempRoot()
        {
            var root = Path.Combine(Application.temporaryCachePath, "rootborn-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
```

- [ ] **Step 2: Run the failing test**

Run: `tests_run(EditMode, testClass=SaveSlotServiceTests, includeMessages=true, includeStacktrace=true)`

Expected: FAIL because `SaveSlotMetadata`, `SaveSlotSummary`, overloaded `SaveService` constructor, and slot APIs do not exist.

- [ ] **Step 3: Implement metadata DTOs**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`:

```csharp
using System;
using Rootborn.Game.Player;

namespace Rootborn.Game.Save
{
    [Serializable]
    public sealed class SaveSlotMetadata
    {
        public string SlotId;
        public string DisplayName;
        public long CreatedAtUtcTicks;
        public long UpdatedAtUtcTicks;
        public int WorldSeed;
        public int TileSeed;
        public CharacterCustomization Character = new CharacterCustomization();
    }

    public sealed class SaveSlotSummary
    {
        public SaveSlotSummary(string slotId, SaveSlotMetadata metadata)
        {
            SlotId = slotId;
            Metadata = metadata;
        }

        public string SlotId { get; }
        public bool Exists => Metadata != null;
        public SaveSlotMetadata Metadata { get; }
    }

    public static class ActiveSaveContext
    {
        public static SaveSlotMetadata Metadata { get; private set; }
        public static string SlotId => Metadata != null ? Metadata.SlotId : string.Empty;

        public static void Set(SaveSlotMetadata metadata)
        {
            Metadata = metadata;
        }

        public static void Clear()
        {
            Metadata = null;
        }
    }
}
```

- [ ] **Step 4: Extend SaveService**

Use Unity MCP `script-update-or-create` to replace `Assets/Scripts/Game/Save/SaveService.cs` with an implementation that keeps `WriteJson`/`ReadJson` and adds:

```csharp
public const int MaxUiSlots = 3;
private const string MetadataFileName = "metadata.json";

public SaveService(string slot) : this(slot, Path.Combine(Application.persistentDataPath, "saves")) { }

public SaveService(string slot, string rootDirectory)
{
    _slot = SanitizeOrThrow(string.IsNullOrEmpty(slot) ? "default" : slot);
    _rootDir = rootDirectory;
    _dir = Path.Combine(_rootDir, _slot);
    Directory.CreateDirectory(_dir);
}

public IReadOnlyList<SaveSlotSummary> ListUiSlots()
{
    var result = new List<SaveSlotSummary>(MaxUiSlots);
    for (int i = 0; i < MaxUiSlots; i++)
    {
        string slotId = $"slot-{i}";
        result.Add(new SaveSlotSummary(slotId, LoadMetadata(slotId)));
    }
    return result;
}
```

Include `CreateMetadata`, `SaveMetadata`, `LoadMetadata`, `DeleteSlot`, `SlotDirectory`, `SanitizeOrThrow`, and JSON serialization via `JsonUtility`.

- [ ] **Step 5: Run test to verify it passes**

Run: `tests_run(EditMode, testClass=SaveSlotServiceTests, includeMessages=true)`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs Assets/Scripts/Game/Save/SaveSlotMetadata.cs Assets/Scripts/Game/Save/SaveService.cs
git commit -m "[FEATURE][TEST] 세이브 슬롯 메타데이터 저장소 추가"
```

## Task 2: Seeded World Generator Tests

**Files:**

- Create: `Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs`
- Create: `Assets/Scripts/Game/WorldGeneration/*.cs`

- [ ] **Step 1: Write the failing generator test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.Resources;
using Rootborn.Game.WorldGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.WorldGeneration
{
    public sealed class SeededWorldGeneratorTests
    {
        [Test]
        public void Generate_SameSeeds_ProducesSameTilesAndProps()
        {
            var def = MakeDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 10, 20);
            Assert.AreEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_DifferentSeeds_ProducesDifferentTilesOrProps()
        {
            var def = MakeDefinition();
            var a = SeededWorldGenerator.Generate(def, 10, 20);
            var b = SeededWorldGenerator.Generate(def, 11, 21);
            Assert.AreNotEqual(a.Signature(), b.Signature());
        }

        [Test]
        public void Generate_RespectsTilePatternCoordinates()
        {
            var def = MakeDefinition();
            var world = SeededWorldGenerator.Generate(def, 10, 20);
            Assert.AreSame(world.GetTile(0, 0), world.GetTile(2, 0));
            Assert.AreSame(world.GetTile(1, 0), world.GetTile(3, 0));
        }

        [Test]
        public void Generate_DoesNotPlacePropsInsideReservedArea()
        {
            var def = MakeDefinition();
            var world = SeededWorldGenerator.Generate(def, 10, 20);
            foreach (var prop in world.Props)
            {
                Assert.IsFalse(prop.Cell.x >= 0 && prop.Cell.x <= 3 && prop.Cell.y >= 0 && prop.Cell.y <= 3);
            }
        }

        private static TerrainGenerationDefinition MakeDefinition()
        {
            var tileA = ScriptableObject.CreateInstance<Tile>();
            var tileB = ScriptableObject.CreateInstance<Tile>();
            var setA = ScriptableObject.CreateInstance<TileVariantSetDefinition>();
            setA.SetTestData(new[] { new WeightedTileVariant(tileA, 1) });
            var setB = ScriptableObject.CreateInstance<TileVariantSetDefinition>();
            setB.SetTestData(new[] { new WeightedTileVariant(tileB, 1) });

            var pattern = ScriptableObject.CreateInstance<TilePatternDefinition>();
            pattern.SetTestData(2, 1, new[] { setA, setB });

            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var spawn = ScriptableObject.CreateInstance<NaturalPropSpawnDefinition>();
            spawn.SetTestData(resource, 8, new RectInt(0, 0, 10, 10), 1, 64);

            var def = ScriptableObject.CreateInstance<TerrainGenerationDefinition>();
            def.SetTestData(10, 10, pattern, new[] { new TerrainReservedArea(new RectInt(0, 0, 4, 4), 0) }, new[] { spawn });
            return def;
        }
    }
}
```

- [ ] **Step 2: Run the failing test**

Run: `tests_run(EditMode, testClass=SeededWorldGeneratorTests, includeMessages=true, includeStacktrace=true)`

Expected: FAIL because world generation types do not exist.

- [ ] **Step 3: Implement generation data and DTOs**

Use Unity MCP `script-update-or-create` for the `Assets/Scripts/Game/WorldGeneration/` files. Required public API:

```csharp
namespace Rootborn.Game.WorldGeneration
{
    [System.Serializable]
    public readonly struct WeightedTileVariant
    {
        public WeightedTileVariant(TileBase tile, int weight) { Tile = tile; Weight = weight; }
        public TileBase Tile { get; }
        public int Weight { get; }
    }
}
```

```csharp
public sealed class GeneratedWorld
{
    public IReadOnlyList<GeneratedTile> Tiles { get; }
    public IReadOnlyList<GeneratedProp> Props { get; }
    public TileBase GetTile(int x, int y)
    {
        for (int i = 0; i < Tiles.Count; i++)
        {
            if (Tiles[i].Cell.x == x && Tiles[i].Cell.y == y)
            {
                return Tiles[i].Tile;
            }
        }
        return null;
    }

    public string Signature()
    {
        var parts = new System.Collections.Generic.List<string>(Tiles.Count + Props.Count);
        for (int i = 0; i < Tiles.Count; i++)
        {
            parts.Add($"T:{Tiles[i].Cell.x}:{Tiles[i].Cell.y}:{Tiles[i].Tile.GetInstanceID()}");
        }
        for (int i = 0; i < Props.Count; i++)
        {
            parts.Add($"P:{Props[i].Cell.x}:{Props[i].Cell.y}:{Props[i].Resource.GetInstanceID()}");
        }
        parts.Sort(System.StringComparer.Ordinal);
        return string.Join("|", parts);
    }
}
```

The implementation must use SO references and arrays only. It must not branch on tile names, resource IDs, or crop/tool IDs.

- [ ] **Step 4: Implement SeededWorldGenerator**

Use deterministic hash-based random values:

```csharp
private static int Hash(int seed, int x, int y, int salt)
{
    unchecked
    {
        int h = seed;
        h = (h * 397) ^ x;
        h = (h * 397) ^ y;
        h = (h * 397) ^ salt;
        return h & 0x7fffffff;
    }
}
```

Generation rules:

- For every `x/y`, ask `TilePatternDefinition.GetVariantSet(x, y)`, then pick tile with `tileSeed`.
- For each `NaturalPropSpawnDefinition`, try deterministic candidate cells until `targetCount` or `maxAttempts`.
- Reject candidate cells inside any `TerrainReservedArea`.
- Reject candidate cells closer than `minDistanceBetweenProps` for the same spawn rule.

- [ ] **Step 5: Run tests to verify pass**

Run: `tests_run(EditMode, testClass=SeededWorldGeneratorTests, includeMessages=true)`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs Assets/Scripts/Game/WorldGeneration
git commit -m "[FEATURE][TEST] 시드 기반 월드 생성기 추가"
```

## Task 3: Wire Save Slot UI Flow

**Files:**

- Create: `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- Modify: `Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs`
- Modify: `Assets/Scripts/UI/CharacterCreator.cs`
- Create: `Assets/Tests/PlayMode/SaveSlotUiFlowTests.cs`

- [ ] **Step 1: Write failing UI test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/PlayMode/SaveSlotUiFlowTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class SaveSlotUiFlowTests
    {
        [UnityTest]
        public IEnumerator SaveSlotSelectPanel_Show_DisplaysThreeSlotCards()
        {
            var panel = SaveSlotSelectPanel.EnsureInScene();
            panel.Show();
            yield return null;

            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-0"));
            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-1"));
            Assert.IsNotNull(GameObject.Find("SaveSlotCard_slot-2"));
        }
    }
}
```

- [ ] **Step 2: Run failing UI test**

Run: `tests_run(PlayMode, testClass=SaveSlotUiFlowTests, includeMessages=true, includeStacktrace=true)`

Expected: FAIL because `SaveSlotSelectPanel` does not exist.

- [ ] **Step 3: Implement SaveSlotSelectPanel**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs` with:

```csharp
public static SaveSlotSelectPanel EnsureInScene()
{
    var existing = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
    if (existing != null) return existing;
    var go = new GameObject("SaveSlotSelectPanel");
    return go.AddComponent<SaveSlotSelectPanel>();
}
```

Required behavior:

- `Show()` calls `EnsureCanvas()` and creates three card GameObjects named `SaveSlotCard_slot-0`, `SaveSlotCard_slot-1`, `SaveSlotCard_slot-2`.
- Existing slot cards have `Load` and `Delete` buttons.
- Empty slot cards have `New Game` button.
- New game creates pending `SaveSlotMetadata` with deterministic random seeds from `System.Environment.TickCount` and stores it in `ActiveSaveContext`.
- Load stores loaded metadata in `ActiveSaveContext` and loads `Farm`.
- Delete calls `SaveService.DeleteSlot(slotId)` and rebuilds the card list.

- [ ] **Step 4: Modify ModeSelectPanel**

Use Unity MCP `script-update-or-create` so `OnSingle()` becomes:

```csharp
private void OnSingle()
{
    ApplyMode(SessionMode.Single);
    var slots = SaveSlotSelectPanel.EnsureInScene();
    slots.Show();
}
```

- [ ] **Step 5: Modify CharacterCreator**

Use Unity MCP `script-update-or-create` to add:

```csharp
private SaveSlotMetadata _pendingMetadata;

public void ShowForSlot(SaveSlotMetadata pendingMetadata)
{
    _pendingMetadata = pendingMetadata;
    Show();
}
```

In `OnStart()`, after filling `_draft`, save `_pendingMetadata` if present:

```csharp
if (_pendingMetadata != null)
{
    _pendingMetadata.Character = _draft;
    var service = new SaveService(_pendingMetadata.SlotId);
    service.SaveMetadata(_pendingMetadata);
    ActiveSaveContext.Set(_pendingMetadata);
}
else
{
    _draft.Save();
}
```

Keep legacy `_draft.Save()` for tests and non-slot flows.

- [ ] **Step 6: Run UI test**

Run: `tests_run(PlayMode, testClass=SaveSlotUiFlowTests, includeMessages=true)`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs Assets/Scripts/UI/CharacterCreator.cs Assets/Tests/PlayMode/SaveSlotUiFlowTests.cs
git commit -m "[UI][FEATURE][TEST] 세이브 슬롯 선택 UI 연결"
```

## Task 4: Register Generation Data

**Files:**

- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Scripts/Editor/Tools/GenerateDefaultData.cs`
- Create assets under `Assets/Data/WorldGeneration/`
- Modify asset: `Assets/Data/Registry/GameDataRegistry.asset`

- [ ] **Step 1: Add registry failing test**

Use Unity MCP `script-update-or-create` to add a test in `Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs`:

```csharp
[Test]
public void Registry_DefaultFarmTerrainGeneration_IsAssigned()
{
    var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>(
        "Assets/Data/Registry/GameDataRegistry.asset");
    Assert.IsNotNull(registry);
    Assert.IsNotNull(registry.DefaultFarmTerrainGeneration);
}
```

- [ ] **Step 2: Run failing test**

Run: `tests_run(EditMode, testClass=SeededWorldGeneratorTests, includeMessages=true)`

Expected: FAIL because registry has no `DefaultFarmTerrainGeneration` property or asset is not assigned.

- [ ] **Step 3: Extend GameDataRegistry**

Use Unity MCP `script-update-or-create` to add:

```csharp
using Rootborn.Game.WorldGeneration;

[SerializeField] private TerrainGenerationDefinition _defaultFarmTerrainGeneration;
public TerrainGenerationDefinition DefaultFarmTerrainGeneration => _defaultFarmTerrainGeneration;
```

- [ ] **Step 4: Create default assets**

Use Unity MCP asset tools or `GenerateDefaultData` editor script to create:

- `Assets/Data/WorldGeneration/TileVariants_Grass_Default.asset`
- `Assets/Data/WorldGeneration/Pattern_Grass_Default.asset`
- `Assets/Data/WorldGeneration/Spawn_Trees_Default.asset`
- `Assets/Data/WorldGeneration/Spawn_Rocks_Default.asset`
- `Assets/Data/WorldGeneration/Terrain_Farm_Default.asset`

Default settings:

- terrain width `30`
- terrain height `20`
- tree target count `12`
- rock target count `8`
- reserved area `RectInt(0, 0, 4, 4)`
- `maxAttempts` at least `256`

- [ ] **Step 5: Register asset**

Use Unity MCP `assets-modify` on `Assets/Data/Registry/GameDataRegistry.asset` after inspecting with `assets-get-data`. Set `_defaultFarmTerrainGeneration` to `Assets/Data/WorldGeneration/Terrain_Farm_Default.asset`.

- [ ] **Step 6: Run registry test**

Run: `tests_run(EditMode, testClass=SeededWorldGeneratorTests, includeMessages=true)`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Scripts/Editor/Tools/GenerateDefaultData.cs Assets/Data/WorldGeneration Assets/Data/Registry/GameDataRegistry.asset Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs
git commit -m "[ASSET][FEATURE][TEST] 기본 농장 생성 데이터 등록"
```

## Task 5: Apply Generated World In FarmAutoFiller

**Files:**

- Modify: `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
- Test: `Assets/Tests/PlayMode/FarmFlowTests.cs`

- [ ] **Step 1: Add failing PlayMode test**

Use Unity MCP `script-update-or-create` to extend `Assets/Tests/PlayMode/FarmFlowTests.cs`:

```csharp
[UnityTest]
public IEnumerator FarmScene_UsesActiveSaveSeeds_ForResourceLayout()
{
    ActiveSaveContext.Set(new SaveSlotMetadata
    {
        SlotId = "slot-0",
        DisplayName = "slot-0",
        WorldSeed = 111,
        TileSeed = 222,
        Character = new CharacterCustomization()
    });

    yield return LoadFarmSceneAndWait();

    var first = ResourceLayoutSignature();

    ActiveSaveContext.Set(new SaveSlotMetadata
    {
        SlotId = "slot-0",
        DisplayName = "slot-0",
        WorldSeed = 333,
        TileSeed = 444,
        Character = new CharacterCustomization()
    });

    yield return LoadFarmSceneAndWait();

    var second = ResourceLayoutSignature();
    Assert.AreNotEqual(first, second);
}
```

`ResourceLayoutSignature()` should sort all `ResourceNode` positions and concatenate `x:y` values.

- [ ] **Step 2: Run failing PlayMode test**

Run: `tests_run(PlayMode, testClass=FarmFlowTests, includeMessages=true, includeStacktrace=true)`

Expected: FAIL because `FarmAutoFiller` still uses fixed seed `20260504`.

- [ ] **Step 3: Modify FarmAutoFiller tile fill**

Use Unity MCP `script-update-or-create` to change `FillIfEmpty()`:

```csharp
var metadata = ActiveSaveContext.Metadata ?? CreateFallbackMetadata();
var generation = registry.DefaultFarmTerrainGeneration;
GeneratedWorld generated = null;
if (generation != null)
{
    generated = SeededWorldGenerator.Generate(generation, metadata.WorldSeed, metadata.TileSeed);
}
EnsureGroundFilled(groundTilemap, registry, data, generated);
EnsureResourceNodes(registry, generated);
```

Keep existing fallback behavior when `generated == null`.

- [ ] **Step 4: Modify resource spawning**

Add an overload:

```csharp
private static void EnsureResourceNodes(GameDataRegistry registry, GeneratedWorld generated)
{
    if (generated == null)
    {
        EnsureResourceNodes(registry);
        return;
    }

    var rootGo = ResetResourceRoot();
    for (int i = 0; i < generated.Props.Count; i++)
    {
        var prop = generated.Props[i];
        SpawnNode(rootGo.transform, prop.Resource, new Vector3(prop.Cell.x + 0.5f, prop.Cell.y + 0.5f, 0f), $"{prop.Resource.name}_{i:00}");
    }
}
```

The name may use `ResourceNodeDefinition.name`; do not branch on `ResourceNodeDefinition.Id`.

- [ ] **Step 5: Run FarmFlowTests**

Run: `tests_run(PlayMode, testClass=FarmFlowTests, includeMessages=true)`

Expected: PASS, including existing resource count assertions.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs Assets/Tests/PlayMode/FarmFlowTests.cs
git commit -m "[FEATURE][TEST] 농장 자동 생성에 세이브 seed 적용"
```

## Task 6: Full Regression And Entity Branching Gate

**Files:** verification task; modify only files that fail the listed checks

- [ ] **Step 1: Run EditMode tests**

Run: `tests_run(EditMode, includeMessages=true, includeLogs=true)`

Expected: PASS

- [ ] **Step 2: Run targeted PlayMode tests**

Run:

- `tests_run(PlayMode, testClass=FarmFlowTests, includeMessages=true, includeLogs=true)`
- `tests_run(PlayMode, testClass=WorldMapFlowTests, includeMessages=true, includeLogs=true)`
- `tests_run(PlayMode, testClass=WorldSceneFlowTests, includeMessages=true, includeLogs=true)`
- `tests_run(PlayMode, testClass=FarmingScenarioTests, includeMessages=true, includeLogs=true)`
- `tests_run(PlayMode, testClass=SaveSlotUiFlowTests, includeMessages=true, includeLogs=true)`

Expected: PASS

- [ ] **Step 3: Run entity branching CI gate**

Run in PowerShell:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
```

Expected: exit code 0

- [ ] **Step 4: Inspect logs**

Run: `console_get_logs(lastMinutes=30, logTypeFilter=Error, maxEntries=100)`

Expected: no compile errors, no scene load errors, no generator null reference errors.

- [ ] **Step 5: Completion audit**

Check every requirement from `docs/superpowers/specs/2026-05-06-save-slots-seeded-world-design.md` against:

- source files
- generated SO assets
- passing test results
- CI gate result

- [ ] **Step 6: Commit final verification fixes**

If fixes were required:

```bash
git add Assets/Scripts/Game Assets/Scripts/UI Assets/Scripts/Editor Assets/Tests Assets/Data
git commit -m "[BUGFIX][TEST] 세이브 슬롯 시드 월드 회귀 수정"
```

If no fixes were required, do not create an empty commit.

## Self-Review Notes

- Spec coverage: save slot UI, metadata persistence, CLI-safe fallback, seed-based tile/prop generation, SO rule data, `FarmAutoFiller` integration, tests, and regression gates all map to tasks above.
- Placeholder scan: no `TBD`, `TODO`, or vague "implement later" steps remain. Every task has concrete files, commands, expected failures, expected passes, and commit boundaries.
- Type consistency: names used across tasks are `SaveSlotMetadata`, `SaveSlotSummary`, `ActiveSaveContext`, `TileVariantSetDefinition`, `TilePatternDefinition`, `TerrainGenerationDefinition`, `NaturalPropSpawnDefinition`, `GeneratedWorld`, and `SeededWorldGenerator`.
