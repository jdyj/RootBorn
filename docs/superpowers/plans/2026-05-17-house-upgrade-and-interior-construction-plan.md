# House Upgrade and Interior Construction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one optional House expansion stage with two routes: hire construction for all players and direct tile construction for interior-eligible players only.

**Architecture:** Add a focused `Rootborn.Game.Housing` domain for upgrade stages, blueprints, route conditions/effects, currency, save data, and transaction orchestration. Reuse the existing House `InteriorTilemapApplier` and interior placement UI patterns, but make saved `HouseStateSaveData` the source of truth for the active room stage.

**Tech Stack:** Unity 6000.3, C#, ScriptableObject data, Unity Tilemaps, existing SaveService JSON files, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for all `Assets/**/*.cs` changes.

---

## Implementation Rules

- Do not write `Assets/**/*.cs` directly with shell redirection or file writes. Use Unity MCP `script-update-or-create` for all C# creation and updates.
- Keep implementation data-driven. Do not branch on `career.interior`, `practice.interior`, or specific stage ids in runtime logic.
- Commit after each completed task using Korean commit messages and valid prefixes.
- For visible House/tilemap changes, complete both automated tests and direct visual PlayMode verification before reporting completion.

## File Structure

- Create `Assets/Scripts/Game/Housing/HouseCurrency.cs`: simple data-driven currency state and transaction result types.
- Create `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs`: stage, blueprint, cell requirement, route condition, and route effect ScriptableObject definitions.
- Create `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`: save data model and JSON persistence through `SaveService`.
- Create `Assets/Scripts/Game/Housing/HouseUpgradeService.cs`: route availability, atomic hire completion, direct construction start, direct construction completion.
- Create `Assets/Scripts/Game/Housing/HouseConstructionSession.cs`: validates blueprint cells and tracks direct construction placement.
- Modify `Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs`: resolve the saved House upgrade stage before generating the map.
- Create `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`: local upgrade selection panel with hire/direct route buttons.
- Create `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`: installs a Town-side upgrade provider and panel for the first slice.
- Create `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`: data validation tests.
- Create `Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs`: transaction and route gating tests.
- Create `Assets/Tests/EditMode/Housing/HouseConstructionSessionTests.cs`: blueprint completion validation tests.
- Create `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`: player-facing route selection and saved stage flow tests.
- Create `Assets/Tests/PlayMode/Housing/HouseDirectConstructionVisualPlayModeTests.cs`: direct construction input/overlay/tilemap flow tests.
- Create data assets under `Assets/Data/Housing/UpgradeStages/`, `Assets/Data/Housing/Blueprints/`, `Assets/Data/Housing/Currency/`, `Assets/Data/Housing/Conditions/`, and `Assets/Data/Housing/Effects/`.
- Modify `Assets/Data/Registry/GameDataRegistry.asset` only through Unity serialization tools or Editor APIs during execution.

---

### Task 1: Currency and Save Foundation

**Files:**
- Create: `Assets/Scripts/Game/Housing/HouseCurrency.cs`
- Create: `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`
- Test: `Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs`

- [ ] **Step 1: Write failing tests for currency and save persistence**

Create `Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs` with:

```csharp
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeServiceTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-upgrade-tests-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void HOUSE_UPGRADE_001_CurrencySpendIsAtomicWhenFundsAreInsufficient()
        {
            var wallet = new HouseCurrencyWallet(50);
            Assert.IsFalse(wallet.TrySpend(80));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_002_HouseStatePersistsStageAndActiveConstruction()
        {
            var state = new HouseStateSaveData
            {
                CurrentStageIndex = 1,
                ActiveConstructionStageId = "house.stage.1",
                LatestRoute = HouseUpgradeRouteKind.DirectConstruction,
                CompletionHistoryIds = new[] { "house.stage.1:direct" }
            };

            HouseStatePersistence.Save("slot-0", state);
            var loaded = HouseStatePersistence.Load("slot-0");

            Assert.AreEqual(1, loaded.CurrentStageIndex);
            Assert.AreEqual("house.stage.1", loaded.ActiveConstructionStageId);
            Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, loaded.LatestRoute);
            Assert.AreEqual("house.stage.1:direct", loaded.CompletionHistoryIds[0]);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests`

Expected: FAIL because `Rootborn.Game.Housing` types do not exist.

- [ ] **Step 3: Create minimal currency and save code**

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseCurrency.cs`:

```csharp
using System;

namespace Rootborn.Game.Housing
{
    public enum HouseUpgradeRouteKind
    {
        None,
        HireConstruction,
        DirectConstruction
    }

    [Serializable]
    public sealed class HouseCurrencySaveData
    {
        public int Balance;
    }

    public sealed class HouseCurrencyWallet
    {
        public HouseCurrencyWallet(int balance)
        {
            Balance = Math.Max(0, balance);
        }

        public int Balance { get; private set; }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount)) return false;
            Balance -= amount;
            return true;
        }

        public void Grant(int amount)
        {
            if (amount > 0) Balance += amount;
        }
    }
}
```

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`:

```csharp
using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    [Serializable]
    public sealed class HouseConstructionCellSaveData
    {
        public int X;
        public int Y;
        public HouseConstructionCellKind Kind;
    }

    [Serializable]
    public sealed class HouseStateSaveData
    {
        public int CurrentStageIndex;
        public string ActiveConstructionStageId;
        public HouseConstructionCellSaveData[] PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
        public HouseUpgradeRouteKind LatestRoute;
        public string[] CompletionHistoryIds = Array.Empty<string>();
        public HouseCurrencySaveData Currency = new HouseCurrencySaveData();
    }

    public static class HouseStatePersistence
    {
        private const string FileName = "house-state.json";

        public static HouseStateSaveData Load(string saveSlot)
        {
            if (string.IsNullOrEmpty(saveSlot)) saveSlot = "default";
            var json = new SaveService(saveSlot).ReadJson(FileName);
            if (string.IsNullOrEmpty(json)) return new HouseStateSaveData();
            var data = JsonUtility.FromJson<HouseStateSaveData>(json);
            return Normalize(data);
        }

        public static void Save(string saveSlot, HouseStateSaveData state)
        {
            if (string.IsNullOrEmpty(saveSlot)) saveSlot = "default";
            var normalized = Normalize(state);
            new SaveService(saveSlot).WriteJson(FileName, JsonUtility.ToJson(normalized, true));
        }

        private static HouseStateSaveData Normalize(HouseStateSaveData state)
        {
            state ??= new HouseStateSaveData();
            state.CurrentStageIndex = Math.Max(0, state.CurrentStageIndex);
            state.PlacedConstructionCells ??= Array.Empty<HouseConstructionCellSaveData>();
            state.CompletionHistoryIds ??= Array.Empty<string>();
            state.Currency ??= new HouseCurrencySaveData();
            return state;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests`

Expected: PASS for the two foundation tests.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/Game/Housing/HouseCurrency.cs Assets/Scripts/Game/Housing/HouseCurrency.cs.meta Assets/Scripts/Game/Housing/HouseStatePersistence.cs Assets/Scripts/Game/Housing/HouseStatePersistence.cs.meta Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs.meta
git commit -m "[FEATURE][TEST] House 확장 저장과 통화 기반 추가"
```

---

### Task 2: Upgrade Definitions and Blueprint Validation

**Files:**
- Create: `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs`
- Create: `Assets/Scripts/Game/Housing/HouseConstructionSession.cs`
- Test: `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`
- Test: `Assets/Tests/EditMode/Housing/HouseConstructionSessionTests.cs`

- [ ] **Step 1: Write failing definition and blueprint tests**

Create `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs` with:

```csharp
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeDefinitionTests
    {
        [Test]
        public void HOUSE_UPGRADE_010_StageDefinitionClampsCostsAndRequiresPositiveStage()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, profile, null, null);

            Assert.AreEqual("house.stage.1", stage.Id);
            Assert.AreEqual(1, stage.StageIndex);
            Assert.AreEqual(300, stage.HireCost);
            Assert.AreEqual(120, stage.DirectCost);
            Assert.AreSame(profile, stage.Profile);
        }

        [Test]
        public void HOUSE_UPGRADE_011_BlueprintRejectsMissingRequiredCells()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            Assert.AreEqual(3, blueprint.RequiredCells.Count);
            Assert.IsFalse(blueprint.IsCellAllowed(new Vector2Int(5, 5), HouseConstructionCellKind.Floor));
            Assert.IsTrue(blueprint.IsCellAllowed(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
        }
    }
}
```

Create `Assets/Tests/EditMode/Housing/HouseConstructionSessionTests.cs` with:

```csharp
using NUnit.Framework;
using Rootborn.Game.Housing;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseConstructionSessionTests
    {
        [Test]
        public void HOUSE_UPGRADE_020_DirectConstructionCompletesOnlyAfterAllRequiredCellsArePlaced()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            var session = new HouseConstructionSession(blueprint);

            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.TryPlace(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
            Assert.IsTrue(session.TryPlace(new Vector2Int(1, 2), HouseConstructionCellKind.Wall));
            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.TryPlace(new Vector2Int(2, 1), HouseConstructionCellKind.Door));
            Assert.IsTrue(session.IsComplete);
        }

        [Test]
        public void HOUSE_UPGRADE_021_DirectConstructionRejectsWrongTileKind()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Wall(1, 2) });

            var session = new HouseConstructionSession(blueprint);

            Assert.IsFalse(session.TryPlace(new Vector2Int(1, 2), HouseConstructionCellKind.Floor));
            Assert.IsFalse(session.IsComplete);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests`

Expected: FAIL because definition types do not exist.

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseConstructionSessionTests`

Expected: FAIL because session types do not exist.

- [ ] **Step 3: Implement definitions and construction session**

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs` with the public API used by the tests:

```csharp
using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public enum HouseConstructionCellKind
    {
        Floor,
        Wall,
        Door,
        Decoration
    }

    [Serializable]
    public readonly struct HouseConstructionCellRequirement
    {
        public HouseConstructionCellRequirement(Vector2Int cell, HouseConstructionCellKind kind)
        {
            Cell = cell;
            Kind = kind;
        }

        public Vector2Int Cell { get; }
        public HouseConstructionCellKind Kind { get; }
        public static HouseConstructionCellRequirement Floor(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Floor);
        public static HouseConstructionCellRequirement Wall(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Wall);
        public static HouseConstructionCellRequirement Door(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Door);
    }

    public readonly struct HouseUpgradeContext
    {
        public HouseUpgradeContext(HouseStateSaveData state, HouseCurrencyWallet wallet, StudentLife.StudentLifeProgress progress)
        {
            State = state;
            Wallet = wallet;
            Progress = progress;
        }

        public HouseStateSaveData State { get; }
        public HouseCurrencyWallet Wallet { get; }
        public StudentLife.StudentLifeProgress Progress { get; }
    }

    public abstract class HouseUpgradeConditionBase : ScriptableObject
    {
        public abstract bool IsMet(in HouseUpgradeContext context);
    }

    public abstract class HouseUpgradeEffectBase : ScriptableObject
    {
        public abstract bool CanApply(in HouseUpgradeContext context);
        public abstract void Apply(in HouseUpgradeContext context);
    }

    [CreateAssetMenu(fileName = "HouseCondition_Always", menuName = "Rootborn/Housing/Conditions/Always")]
    public sealed class HouseAlwaysCondition : HouseUpgradeConditionBase
    {
        public override bool IsMet(in HouseUpgradeContext context) => true;
    }

    [CreateAssetMenu(fileName = "HouseStage_New", menuName = "Rootborn/Housing/Upgrade Stage")]
    public sealed class HouseUpgradeStageDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private int _stageIndex;
        [SerializeField] private int _hireCost;
        [SerializeField] private int _directCost;
        [SerializeField] private InteriorGenerationProfile _profile;
        [SerializeField] private InteriorTileSetDefinition _tileSet;
        [SerializeField] private HouseConstructionBlueprintDefinition _blueprint;
        [SerializeField] private HouseUpgradeConditionBase[] _generalConditions = Array.Empty<HouseUpgradeConditionBase>();
        [SerializeField] private HouseUpgradeConditionBase[] _directConditions = Array.Empty<HouseUpgradeConditionBase>();
        [SerializeField] private HouseUpgradeEffectBase[] _hireEffects = Array.Empty<HouseUpgradeEffectBase>();
        [SerializeField] private HouseUpgradeEffectBase[] _directEffects = Array.Empty<HouseUpgradeEffectBase>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayKey => string.IsNullOrEmpty(_displayKey) ? Id : _displayKey;
        public int StageIndex => Mathf.Max(0, _stageIndex);
        public int HireCost => Mathf.Max(0, _hireCost);
        public int DirectCost => Mathf.Max(0, _directCost);
        public InteriorGenerationProfile Profile => _profile;
        public InteriorTileSetDefinition TileSet => _tileSet;
        public HouseConstructionBlueprintDefinition Blueprint => _blueprint;
        public IReadOnlyList<HouseUpgradeConditionBase> GeneralConditions => _generalConditions ?? Array.Empty<HouseUpgradeConditionBase>();
        public IReadOnlyList<HouseUpgradeConditionBase> DirectConditions => _directConditions ?? Array.Empty<HouseUpgradeConditionBase>();
        public IReadOnlyList<HouseUpgradeEffectBase> HireEffects => _hireEffects ?? Array.Empty<HouseUpgradeEffectBase>();
        public IReadOnlyList<HouseUpgradeEffectBase> DirectEffects => _directEffects ?? Array.Empty<HouseUpgradeEffectBase>();

        public static HouseUpgradeStageDefinition CreateForTests(string id, int stageIndex, int hireCost, int directCost, InteriorGenerationProfile profile, InteriorTileSetDefinition tileSet, HouseConstructionBlueprintDefinition blueprint)
        {
            var definition = CreateInstance<HouseUpgradeStageDefinition>();
            definition._id = id;
            definition._stageIndex = stageIndex;
            definition._hireCost = hireCost;
            definition._directCost = directCost;
            definition._profile = profile;
            definition._tileSet = tileSet;
            definition._blueprint = blueprint;
            return definition;
        }
    }

    [CreateAssetMenu(fileName = "HouseBlueprint_New", menuName = "Rootborn/Housing/Construction Blueprint")]
    public sealed class HouseConstructionBlueprintDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private RectInt _bounds;
        [SerializeField] private HouseConstructionCellRequirement[] _requiredCells = Array.Empty<HouseConstructionCellRequirement>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public RectInt Bounds => _bounds;
        public IReadOnlyList<HouseConstructionCellRequirement> RequiredCells => _requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();

        public bool IsCellAllowed(Vector2Int cell, HouseConstructionCellKind kind)
        {
            if (!_bounds.Contains(cell)) return false;
            for (int i = 0; i < RequiredCells.Count; i++)
            {
                var required = RequiredCells[i];
                if (required.Cell == cell && required.Kind == kind) return true;
            }
            return false;
        }

        public static HouseConstructionBlueprintDefinition CreateForTests(string id, RectInt bounds, HouseConstructionCellRequirement[] requiredCells)
        {
            var definition = CreateInstance<HouseConstructionBlueprintDefinition>();
            definition._id = id;
            definition._bounds = bounds;
            definition._requiredCells = requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();
            return definition;
        }
    }
}
```

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseConstructionSession.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public sealed class HouseConstructionSession
    {
        private readonly HouseConstructionBlueprintDefinition _blueprint;
        private readonly Dictionary<Vector2Int, HouseConstructionCellKind> _placed = new Dictionary<Vector2Int, HouseConstructionCellKind>();

        public HouseConstructionSession(HouseConstructionBlueprintDefinition blueprint)
        {
            _blueprint = blueprint;
        }

        public bool IsComplete => _blueprint != null && HasAllRequiredCells();

        public bool TryPlace(Vector2Int cell, HouseConstructionCellKind kind)
        {
            if (_blueprint == null || !_blueprint.IsCellAllowed(cell, kind)) return false;
            _placed[cell] = kind;
            return true;
        }

        public HouseConstructionCellSaveData[] ToSaveData()
        {
            var result = new HouseConstructionCellSaveData[_placed.Count];
            int index = 0;
            foreach (var pair in _placed)
            {
                result[index++] = new HouseConstructionCellSaveData { X = pair.Key.x, Y = pair.Key.y, Kind = pair.Value };
            }
            return result;
        }

        private bool HasAllRequiredCells()
        {
            var required = _blueprint.RequiredCells;
            for (int i = 0; i < required.Count; i++)
            {
                var cell = required[i];
                if (!_placed.TryGetValue(cell.Cell, out var kind) || kind != cell.Kind) return false;
            }
            return required.Count > 0;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run both:

```powershell
unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests
unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseConstructionSessionTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs.meta Assets/Scripts/Game/Housing/HouseConstructionSession.cs Assets/Scripts/Game/Housing/HouseConstructionSession.cs.meta Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs.meta Assets/Tests/EditMode/Housing/HouseConstructionSessionTests.cs Assets/Tests/EditMode/Housing/HouseConstructionSessionTests.cs.meta
git commit -m "[FEATURE][TEST] House 확장 단계와 시공 청사진 정의 추가"
```

---

### Task 3: Route Availability and Atomic Upgrade Service

**Files:**
- Create: `Assets/Scripts/Game/Housing/HouseUpgradeService.cs`
- Modify: `Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs`

- [ ] **Step 1: Add failing service tests**

Append these tests to `HouseUpgradeServiceTests`:

```csharp
[Test]
public void HOUSE_UPGRADE_030_HireRouteRejectsInsufficientFundsWithoutStageChange()
{
    var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
    var state = new HouseStateSaveData();
    var wallet = new HouseCurrencyWallet(100);
    var service = new HouseUpgradeService(new[] { stage });

    var result = service.TryHire(stage, state, wallet);

    Assert.AreEqual(HouseUpgradeResultKind.InsufficientCurrency, result.Kind);
    Assert.AreEqual(0, state.CurrentStageIndex);
    Assert.AreEqual(100, wallet.Balance);
}

[Test]
public void HOUSE_UPGRADE_031_HireRoutePaysAndRaisesStageAtomically()
{
    var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
    var state = new HouseStateSaveData();
    var wallet = new HouseCurrencyWallet(500);
    var service = new HouseUpgradeService(new[] { stage });

    var result = service.TryHire(stage, state, wallet);

    Assert.AreEqual(HouseUpgradeResultKind.Applied, result.Kind);
    Assert.AreEqual(1, state.CurrentStageIndex);
    Assert.AreEqual(200, wallet.Balance);
    Assert.AreEqual(HouseUpgradeRouteKind.HireConstruction, state.LatestRoute);
}

[Test]
public void HOUSE_UPGRADE_032_DirectRouteIsUnavailableWithoutDirectCondition()
{
    var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
    var state = new HouseStateSaveData();
    var wallet = new HouseCurrencyWallet(500);
    var service = new HouseUpgradeService(new[] { stage });

    Assert.IsFalse(service.CanStartDirect(stage, new HouseUpgradeContext(state, wallet, null)));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests`

Expected: FAIL because `HouseUpgradeService` and result types do not exist.

- [ ] **Step 3: Implement service**

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseUpgradeService.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Rootborn.Game.Housing
{
    public enum HouseUpgradeResultKind
    {
        Applied,
        InvalidStage,
        AlreadyApplied,
        InsufficientCurrency,
        RequirementFailed,
        ConstructionIncomplete
    }

    public readonly struct HouseUpgradeResult
    {
        public HouseUpgradeResult(HouseUpgradeResultKind kind, string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public HouseUpgradeResultKind Kind { get; }
        public string Message { get; }
    }

    public sealed class HouseUpgradeService
    {
        private readonly HouseUpgradeStageDefinition[] _stages;

        public HouseUpgradeService(IReadOnlyList<HouseUpgradeStageDefinition> stages)
        {
            if (stages == null)
            {
                _stages = Array.Empty<HouseUpgradeStageDefinition>();
                return;
            }

            _stages = new HouseUpgradeStageDefinition[stages.Count];
            for (int i = 0; i < stages.Count; i++) _stages[i] = stages[i];
        }

        public HouseUpgradeStageDefinition ResolveNextStage(HouseStateSaveData state)
        {
            int current = state == null ? 0 : state.CurrentStageIndex;
            HouseUpgradeStageDefinition best = null;
            for (int i = 0; i < _stages.Length; i++)
            {
                var stage = _stages[i];
                if (stage == null || stage.StageIndex <= current) continue;
                if (best == null || stage.StageIndex < best.StageIndex) best = stage;
            }
            return best;
        }

        public bool CanStartDirect(HouseUpgradeStageDefinition stage, in HouseUpgradeContext context)
        {
            if (stage == null || stage.Blueprint == null) return false;
            var conditions = stage.DirectConditions;
            if (conditions.Count == 0) return false;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].IsMet(in context)) return false;
            }
            return true;
        }

        public HouseUpgradeResult TryHire(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet)
        {
            if (stage == null || state == null || wallet == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid stage");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!wallet.CanSpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.HireConstruction;
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }

        public HouseUpgradeResult TryCompleteDirect(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, HouseConstructionSession session)
        {
            if (stage == null || state == null || wallet == null || session == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid direct construction");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!session.IsComplete) return new HouseUpgradeResult(HouseUpgradeResultKind.ConstructionIncomplete, "Construction incomplete");
            if (!wallet.CanSpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.DirectConstruction;
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/Game/Housing/HouseUpgradeService.cs Assets/Scripts/Game/Housing/HouseUpgradeService.cs.meta Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs
git commit -m "[FEATURE][TEST] House 확장 결제와 루트 판정 추가"
```

---

### Task 4: Saved Stage Selection in House Generation

**Files:**
- Modify: `Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs`
- Test: `Assets/Tests/EditMode/Housing/HouseUpgradeServiceTests.cs`
- Test: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode test for saved stage profile**

Create `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs` with:

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Housing
{
    public sealed class HouseUpgradeFlowPlayModeTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-upgrade-playmode-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-0", DisplayName = "slot-0", WorldSeed = 1205, TileSeed = 1205 });
        }

        [TearDown]
        public void TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_001_SavedStageOneGeneratesLargerHouseThanStageZero()
        {
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { CurrentStageIndex = 1 });

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(floor);
            Assert.Greater(CountTiles(floor), 180, "Stage 1 House should generate a visibly larger floor area than the compact starting room.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null) count++;
            }
            return count;
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests`

Expected: FAIL because House generation does not resolve saved stages yet.

- [ ] **Step 3: Add stage profile resolution to `InteriorTilemapApplier`**

Use `script-update-or-create` to modify `InteriorTilemapApplier.cs`:

```csharp
// Add using:
using Rootborn.Game.Housing;

// Add serialized fields:
[SerializeField] private InteriorGenerationProfile _stageOneProfile;
[SerializeField] private InteriorTileSetDefinition _stageOneTileSet;

// Replace profile/tileSet resolution in ApplyGeneratedInterior:
var state = HouseStatePersistence.Load(ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.SlotId : "default");
var profile = ResolveProfileForStage(state.CurrentStageIndex);
var originalTileSet = _tileSet;
_tileSet = ResolveTileSetForStage(state.CurrentStageIndex);
var map = InteriorGenerator.Generate(profile, seed);
Apply(map);
_tileSet = originalTileSet;

// Add helpers:
private InteriorGenerationProfile ResolveProfileForStage(int stageIndex)
{
    if (stageIndex >= 1 && _stageOneProfile != null)
    {
        return _stageOneProfile;
    }

    return _profile != null ? _profile : InteriorGenerationProfile.CreateDefaultOfficeForTests();
}

private InteriorTileSetDefinition ResolveTileSetForStage(int stageIndex)
{
    if (stageIndex >= 1 && _stageOneTileSet != null)
    {
        return _stageOneTileSet;
    }

    return _tileSet;
}
```

If scene assets do not have `_stageOneProfile` wired yet, temporarily fall back by creating a larger runtime profile through a test-only factory in `InteriorGenerationProfile`. Add a public static `CreateExpandedOfficeForTests()` with size `34x22` and use it only when no asset is assigned.

- [ ] **Step 4: Run House generation tests**

Run:

```powershell
unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests
unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Interiors.HouseInteriorGenerationPlayModeTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs.meta
git commit -m "[FEATURE][TEST] 저장된 House 확장 단계 생성 연결"
```

---

### Task 5: Upgrade Offer UI and Route Gating

**Files:**
- Create: `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`
- Create: `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`
- Test: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode route visibility tests**

Append to `HouseUpgradeFlowPlayModeTests`:

```csharp
[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_002_NonInteriorPlayerSeesOnlyHireRoute()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var panel = Object.FindFirstObjectByType<Rootborn.UI.Housing.HouseUpgradePanel>(FindObjectsInactive.Include);
    Assert.IsNotNull(panel);
    panel.ShowForTests(false);

    Assert.IsTrue(panel.HireButtonVisibleForTests);
    Assert.IsFalse(panel.DirectButtonVisibleForTests);
}

[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_003_InteriorEligiblePlayerSeesHireAndDirectRoutes()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var panel = Object.FindFirstObjectByType<Rootborn.UI.Housing.HouseUpgradePanel>(FindObjectsInactive.Include);
    Assert.IsNotNull(panel);
    panel.ShowForTests(true);

    Assert.IsTrue(panel.HireButtonVisibleForTests);
    Assert.IsTrue(panel.DirectButtonVisibleForTests);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests`

Expected: FAIL because UI types do not exist.

- [ ] **Step 3: Implement minimal panel and runtime installer**

Use `script-update-or-create` to create `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseUpgradePanel : MonoBehaviour
    {
        private GameObject _hireButton;
        private GameObject _directButton;

        public bool HireButtonVisibleForTests => _hireButton != null && _hireButton.activeSelf;
        public bool DirectButtonVisibleForTests => _directButton != null && _directButton.activeSelf;

        public static HouseUpgradePanel Create(Canvas canvas)
        {
            var root = new GameObject("HouseUpgradePanel", typeof(RectTransform), typeof(HouseUpgradePanel));
            root.transform.SetParent(canvas.transform, false);
            var panel = root.GetComponent<HouseUpgradePanel>();
            panel.Build();
            root.SetActive(false);
            return panel;
        }

        public void ShowForTests(bool directEligible)
        {
            Show(directEligible);
        }

        public void Show(bool directEligible)
        {
            if (_hireButton == null) Build();
            gameObject.SetActive(true);
            _hireButton.SetActive(true);
            _directButton.SetActive(directEligible);
        }

        private void Build()
        {
            _hireButton = MakeButton("HireConstructionButton", "Hire", new Vector2(-90f, 0f));
            _directButton = MakeButton("DirectConstructionButton", "Direct", new Vector2(90f, 0f));
        }

        private GameObject MakeButton(string name, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(150f, 44f);
            rect.anchoredPosition = anchoredPosition;
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ((RectTransform)textGo.transform).sizeDelta = rect.sizeDelta;
            return go;
        }
    }
}
```

Use `script-update-or-create` to create `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseUpgradeRuntimeInstaller : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != "Town") return;
            if (FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include) != null) return;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HouseUpgradeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            HouseUpgradePanel.Create(canvas);
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests`

Expected: PASS for route visibility tests.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/UI/Housing/HouseUpgradePanel.cs Assets/Scripts/UI/Housing/HouseUpgradePanel.cs.meta Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs.meta Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs
git commit -m "[UI][TEST] House 확장 선택 패널과 루트 표시 추가"
```

---

### Task 6: Direct Construction Player Flow

**Files:**
- Modify: `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- Create: `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`
- Test: `Assets/Tests/PlayMode/Housing/HouseDirectConstructionVisualPlayModeTests.cs`

- [ ] **Step 1: Write failing direct construction visual test**

Create `Assets/Tests/PlayMode/Housing/HouseDirectConstructionVisualPlayModeTests.cs` with:

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using Rootborn.UI.Housing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Housing
{
    public sealed class HouseDirectConstructionVisualPlayModeTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-direct-visual-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-0", DisplayName = "slot-0", WorldSeed = 1205, TileSeed = 1205 });
        }

        [TearDown]
        public void TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_010_DirectConstructionPlacesRequiredCellsAndPersistsStage()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var overlay = HouseConstructionOverlay.EnsureForTests(MakeBlueprint());
            Assert.IsNotNull(overlay);

            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 2), HouseConstructionCellKind.Wall));
            Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(2, 1), HouseConstructionCellKind.Door));
            Assert.IsTrue(overlay.TryCompleteForTests("slot-0", 1));

            var loaded = HouseStatePersistence.Load("slot-0");
            Assert.AreEqual(1, loaded.CurrentStageIndex);
        }

        private static HouseConstructionBlueprintDefinition MakeBlueprint()
        {
            return HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseDirectConstructionVisualPlayModeTests`

Expected: FAIL because `HouseConstructionOverlay` does not exist.

- [ ] **Step 3: Implement constrained construction overlay**

Use `script-update-or-create` to create `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`:

```csharp
using Rootborn.Game.Housing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Housing
{
    public sealed class HouseConstructionOverlay : MonoBehaviour
    {
        private HouseConstructionSession _session;

        public static HouseConstructionOverlay EnsureForTests(HouseConstructionBlueprintDefinition blueprint)
        {
            var existing = FindFirstObjectByType<HouseConstructionOverlay>();
            var overlay = existing != null ? existing : Create();
            overlay.Bind(blueprint);
            return overlay;
        }

        public void Bind(HouseConstructionBlueprintDefinition blueprint)
        {
            _session = new HouseConstructionSession(blueprint);
        }

        public bool TryPlaceForTests(Vector2Int cell, HouseConstructionCellKind kind)
        {
            return _session != null && _session.TryPlace(cell, kind);
        }

        public bool TryCompleteForTests(string saveSlot, int stageIndex)
        {
            if (_session == null || !_session.IsComplete) return false;
            var state = HouseStatePersistence.Load(saveSlot);
            state.CurrentStageIndex = Mathf.Max(state.CurrentStageIndex, stageIndex);
            state.LatestRoute = HouseUpgradeRouteKind.DirectConstruction;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = System.Array.Empty<HouseConstructionCellSaveData>();
            HouseStatePersistence.Save(saveSlot, state);
            return true;
        }

        private static HouseConstructionOverlay Create()
        {
            var go = new GameObject("HouseConstructionOverlay", typeof(HouseConstructionOverlay));
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            return go.GetComponent<HouseConstructionOverlay>();
        }
    }
}
```

- [ ] **Step 4: Run direct construction test**

Run: `unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseDirectConstructionVisualPlayModeTests`

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs.meta Assets/Tests/PlayMode/Housing/HouseDirectConstructionVisualPlayModeTests.cs Assets/Tests/PlayMode/Housing/HouseDirectConstructionVisualPlayModeTests.cs.meta
git commit -m "[UI][TEST] House 직접 시공 오버레이 흐름 추가"
```

---

### Task 7: Author First Slice Data Assets

**Files:**
- Create assets under `Assets/Data/Housing/`
- Modify: `Assets/Data/Registry/GameDataRegistry.asset`
- Test: `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`

- [ ] **Step 1: Add failing registry/data asset test**

Append to `HouseUpgradeDefinitionTests`:

```csharp
#if UNITY_EDITOR
using UnityEditor;
#endif

[Test]
public void HOUSE_UPGRADE_040_FirstSliceAssetsExist()
{
#if UNITY_EDITOR
    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset"));
    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HouseConstructionBlueprintDefinition>("Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset"));
#else
    Assert.Pass();
#endif
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests`

Expected: FAIL because assets do not exist.

- [ ] **Step 3: Create folders and assets through Unity Editor APIs**

Use Unity MCP asset creation or an Editor script execution, not manual YAML writing, to create:

- `Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset`
- `Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset`
- `Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset`
- `Assets/Data/Housing/Effects/HouseEffect_DirectConstructionSkillReward.asset`

Set the stage to:

- id: `house.stage.expanded_room.01`
- stage index: `1`
- hire cost: `300`
- direct cost: `120`
- blueprint: `HouseBlueprint_ExpandedRoom_01`
- direct condition: `HouseCondition_InteriorEligible_Test`
- direct effect: `HouseEffect_DirectConstructionSkillReward`

Set the blueprint to:

- bounds: `(0, 0, 4, 4)`
- required floor: `(1, 1)`
- required wall: `(1, 2)`
- required door: `(2, 1)`

- [ ] **Step 4: Run asset test**

Run: `unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests`

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add -- Assets/Data/Housing Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs
git commit -m "[ASSET][TEST] House 첫 확장 단계와 시공 청사진 추가"
```

---

### Task 8: Verification Gate

**Files:**
- No required code files.
- Evidence under `Builds/Logs/house-upgrade-verification/`.

- [ ] **Step 1: Run focused EditMode tests**

Run:

```powershell
unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests
unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests
unity-mcp-cli tests-run --testMode EditMode --class Rootborn.Tests.EditMode.Housing.HouseConstructionSessionTests
```

Expected: PASS.

- [ ] **Step 2: Run focused PlayMode tests**

Run:

```powershell
unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests
unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Housing.HouseDirectConstructionVisualPlayModeTests
unity-mcp-cli tests-run --testMode PlayMode --class Rootborn.Tests.PlayMode.Interiors.HouseInteriorGenerationPlayModeTests
```

Expected: PASS.

- [ ] **Step 3: Run entity branching CI gate**

Run: `bash Scripts/ci/check-no-entity-id-branching.sh`

Expected: PASS. No `career.interior`, `house.stage.expanded_room.01`, or similar entity id branching should be reported from C# runtime code.

- [ ] **Step 4: Direct visual PlayMode verification**

Use the player-facing flow:

1. Start PlayMode in Town.
2. Open the House upgrade provider or panel.
3. Verify non-interior route shows hire only.
4. Enable an interior-eligible test state through data conditions.
5. Verify hire and direct routes are both visible.
6. Select direct construction.
7. Enter House construction mode.
8. Place the required floor, wall, and door cells.
9. Complete construction.
10. Capture Game View or Camera screenshot to `Builds/Logs/house-upgrade-verification/direct-construction-complete.png`.
11. Inspect runtime tilemap state:
    - selected tile names
    - placed cell positions
    - overlay valid/invalid state
    - no stale sample/debug tilemaps or placeholder objects visible
12. Reload the save and House scene.
13. Capture `Builds/Logs/house-upgrade-verification/reloaded-expanded-house.png`.
14. Confirm saved `house-state.json` has `CurrentStageIndex = 1`.

- [ ] **Step 5: Commit verification notes**

Create `docs/superpowers/audits/2026-05-17-house-upgrade-verification.md` with commands, pass/fail results, screenshot paths, and any blocked verification. Then run:

```powershell
git add -- docs/superpowers/audits/2026-05-17-house-upgrade-verification.md Builds/Logs/house-upgrade-verification
git commit -m "[DOCS][TEST] House 확장 직접 검증 결과 기록"
```

---

## Self-Review

Spec coverage:

- Optional House expansion: Tasks 3, 4, 5.
- Hire route for all players: Tasks 3 and 5.
- Direct construction gated to interior route: Tasks 2, 3, 5, 7.
- Blueprint-constrained tile construction: Tasks 2 and 6.
- Atomic payment and idempotency foundation: Tasks 1 and 3.
- Saved stage as source of truth: Tasks 1 and 4.
- Direct visual verification gate: Task 8.
- Performance constraints: Task 6 keeps validation bounded to blueprint cells; Task 8 verifies no excessive runtime artifacts.

Placeholder scan:

- The plan avoids unresolved marker text and defers no required first-slice behavior.
- The only conditional future work is explicitly outside the first slice: pay-on-start is not part of this implementation.

Type consistency:

- `HouseUpgradeRouteKind`, `HouseStateSaveData`, `HouseConstructionCellKind`, `HouseConstructionCellRequirement`, `HouseConstructionBlueprintDefinition`, `HouseConstructionSession`, and `HouseUpgradeService` are introduced before later tasks reference them.
- Test method names use the `HOUSE_UPGRADE` scenario prefix consistently.
