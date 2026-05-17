# House Upgrade Next Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first player-facing House expansion slice: route selection UI, hire completion, direct construction mode, first-slice data assets, and direct PlayMode visual verification.

**Architecture:** Keep `Rootborn.Game.Housing` as the domain layer for state, definitions, blueprints, and transactions. Add a small `Rootborn.UI.Housing` layer for the upgrade panel and construction overlay, using existing House tilemaps and placement camera behavior. First-slice assets live under `Assets/Data/Housing/` and drive runtime behavior without entity-id branching.

**Tech Stack:** Unity 6000.3, C#, ScriptableObject data, Unity Tilemaps, Unity Input System, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for every `Assets/**/*.cs` change.

---

## Scope and Existing Baseline

Already committed:

- `Assets/Scripts/Game/Housing/HouseCurrency.cs`
- `Assets/Scripts/Game/Housing/HouseStatePersistence.cs`
- `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs`
- `Assets/Scripts/Game/Housing/HouseConstructionSession.cs`
- `Assets/Scripts/Game/Housing/HouseUpgradeService.cs`
- `Assets/Scripts/Game/Interiors/InteriorGeneration.cs`
- `Assets/Scripts/Game/Interiors/InteriorTilemapApplier.cs`
- `Assets/Tests/EditMode/Housing/*`
- `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

Do not rewrite those systems unless a task explicitly modifies them.

## File Structure

- Create `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`: route selection UI, button visibility, button click callbacks, test-facing inspection properties.
- Create `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`: installs the Town-side upgrade provider/panel and supplies first-slice test data when asset registry wiring is not yet present.
- Create `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`: House-only construction mode overlay, blueprint cell markers, placement progress, completion/cancel actions.
- Create `Assets/Scripts/Game/Housing/HouseUpgradeAssetFactory.cs`: Editor/test helper for first-slice data asset creation through Unity APIs.
- Modify `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs`: add safe test/configuration methods for setting condition/effect arrays and serialized fields through Unity-created assets.
- Modify `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`: validate first-slice assets.
- Modify `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`: add player-facing panel, hire, direct construction, save/reload tests.
- Create assets under `Assets/Data/Housing/`.
- Create `docs/superpowers/audits/2026-05-17-house-upgrade-next-slice-verification.md`: final verification evidence.

## Task 1: Upgrade Panel Route Visibility

**Files:**
- Create: `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`
- Create: `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`
- Modify: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode tests for route visibility**

Append these tests to `HouseUpgradeFlowPlayModeTests`:

```csharp
[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_002_NonInteriorPlayerSeesHireRouteOnly()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var panel = Object.FindFirstObjectByType<Rootborn.UI.Housing.HouseUpgradePanel>(FindObjectsInactive.Include);
    Assert.IsNotNull(panel, "Town should install the House upgrade panel through the runtime provider flow.");

    panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: false), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: false);

    Assert.IsTrue(panel.HireButtonVisibleForTests);
    Assert.IsTrue(panel.HireButtonInteractableForTests);
    Assert.IsFalse(panel.DirectButtonVisibleForTests);
    StringAssert.Contains("300", panel.VisibleTextForTests);
}

[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_003_InteriorEligiblePlayerSeesHireAndDirectRoutes()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var panel = Object.FindFirstObjectByType<Rootborn.UI.Housing.HouseUpgradePanel>(FindObjectsInactive.Include);
    Assert.IsNotNull(panel);

    panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: true), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: true);

    Assert.IsTrue(panel.HireButtonVisibleForTests);
    Assert.IsTrue(panel.DirectButtonVisibleForTests);
    Assert.IsTrue(panel.DirectButtonInteractableForTests);
    StringAssert.Contains("120", panel.VisibleTextForTests);
}
```

Add this helper in the same test class:

```csharp
private static HouseUpgradeStageDefinition CreateStageForPanelTests(bool includeDirectCondition)
{
    var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
        "blueprint.panel",
        new RectInt(0, 0, 3, 3),
        new[] { HouseConstructionCellRequirement.Floor(1, 1) });
    var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.panel", 1, 300, 120, Rootborn.Game.Interiors.InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
    if (includeDirectCondition)
    {
        stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
    }

    return stage;
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Housing","testClass":"HouseUpgradeFlowPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: FAIL because `Rootborn.UI.Housing.HouseUpgradePanel` does not exist.

- [ ] **Step 3: Add test configuration helpers to `HouseUpgradeStageDefinition`**

Use `script-update-or-create` for `Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs`. Add this method inside `HouseUpgradeStageDefinition`:

```csharp
public void ConfigureConditionsForTests(HouseUpgradeConditionBase[] generalConditions, HouseUpgradeConditionBase[] directConditions)
{
    _generalConditions = generalConditions ?? Array.Empty<HouseUpgradeConditionBase>();
    _directConditions = directConditions ?? Array.Empty<HouseUpgradeConditionBase>();
}

public void ConfigureEffectsForTests(HouseUpgradeEffectBase[] hireEffects, HouseUpgradeEffectBase[] directEffects)
{
    _hireEffects = hireEffects ?? Array.Empty<HouseUpgradeEffectBase>();
    _directEffects = directEffects ?? Array.Empty<HouseUpgradeEffectBase>();
}
```

- [ ] **Step 4: Implement `HouseUpgradePanel`**

Use `script-update-or-create` to create `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`:

```csharp
using Rootborn.Game.Housing;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseUpgradePanel : MonoBehaviour
    {
        private Text _summaryText;
        private Button _hireButton;
        private Button _directButton;
        private Button _closeButton;
        private HouseUpgradeStageDefinition _stage;
        private HouseStateSaveData _state;
        private HouseCurrencyWallet _wallet;

        public bool HireButtonVisibleForTests => _hireButton != null && _hireButton.gameObject.activeSelf;
        public bool DirectButtonVisibleForTests => _directButton != null && _directButton.gameObject.activeSelf;
        public bool HireButtonInteractableForTests => _hireButton != null && _hireButton.interactable;
        public bool DirectButtonInteractableForTests => _directButton != null && _directButton.interactable;
        public string VisibleTextForTests => _summaryText != null ? _summaryText.text : string.Empty;

        public event System.Action<HouseUpgradeStageDefinition, HouseStateSaveData, HouseCurrencyWallet> HireRequested;
        public event System.Action<HouseUpgradeStageDefinition, HouseStateSaveData, HouseCurrencyWallet> DirectRequested;

        public static HouseUpgradePanel EnsureInScene(Canvas canvas)
        {
            var existing = FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var root = new GameObject("HouseUpgradePanel", typeof(RectTransform), typeof(Image), typeof(HouseUpgradePanel));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 260f);
            root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

            var panel = root.GetComponent<HouseUpgradePanel>();
            panel.Build();
            root.SetActive(false);
            return panel;
        }

        public void ShowForTests(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, bool directEligible)
        {
            Show(stage, state, wallet, directEligible);
        }

        public void Show(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, bool directEligible)
        {
            if (_summaryText == null) Build();
            _stage = stage;
            _state = state;
            _wallet = wallet;
            gameObject.SetActive(true);

            bool hasStage = stage != null;
            bool canHire = hasStage && wallet != null && wallet.CanSpend(stage.HireCost);
            bool canDirect = hasStage && directEligible && wallet != null && wallet.CanSpend(stage.DirectCost);

            _summaryText.text = hasStage
                ? $"House Expansion\nHire: {stage.HireCost}\nDirect: {stage.DirectCost}\nBalance: {(wallet != null ? wallet.Balance : 0)}"
                : "No House expansion available.";

            _hireButton.gameObject.SetActive(hasStage);
            _hireButton.interactable = canHire;
            _directButton.gameObject.SetActive(hasStage && directEligible);
            _directButton.interactable = canDirect;
        }

        private void Build()
        {
            _summaryText = MakeText("Summary", new Vector2(0f, 62f), new Vector2(460f, 110f), 22);
            _hireButton = MakeButton("HireConstructionButton", "Hire", new Vector2(-115f, -52f));
            _directButton = MakeButton("DirectConstructionButton", "Direct", new Vector2(115f, -52f));
            _closeButton = MakeButton("CloseHouseUpgradeButton", "Close", new Vector2(0f, -112f));
            _hireButton.onClick.AddListener(() => HireRequested?.Invoke(_stage, _state, _wallet));
            _directButton.onClick.AddListener(() => DirectRequested?.Invoke(_stage, _state, _wallet));
            _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private Text MakeText(string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button MakeButton(string name, string label, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(180f, 46f);
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(0.88f, 0.88f, 0.82f, 1f);
            MakeText("Text", Vector2.zero, rect.sizeDelta, 18).transform.SetParent(go.transform, false);
            go.GetComponentInChildren<Text>().text = label;
            go.GetComponentInChildren<Text>().color = Color.black;
            return go.GetComponent<Button>();
        }
    }
}
```

- [ ] **Step 5: Implement `HouseUpgradeRuntimeInstaller`**

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
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("HouseUpgradeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            }

            HouseUpgradePanel.EnsureInScene(canvas);
        }
    }
}
```

- [ ] **Step 6: Run route visibility tests**

Run the same `HouseUpgradeFlowPlayModeTests` command from Step 2.

Expected: PASS for `HOUSE_UPGRADE_PM_002` and `HOUSE_UPGRADE_PM_003`.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs Assets/Scripts/UI/Housing/HouseUpgradePanel.cs Assets/Scripts/UI/Housing/HouseUpgradePanel.cs.meta Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs.meta Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs
git commit -m "[UI][TEST] House 확장 선택 패널 추가"
```

## Task 2: First-Slice Housing Data Assets

**Files:**
- Create: `Assets/Scripts/Game/Housing/HouseUpgradeAssetFactory.cs`
- Create: `Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset`
- Create: `Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset`
- Create: `Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset`
- Create: `Assets/Data/Housing/Effects/HouseEffect_DirectConstructionReward.asset`
- Modify: `Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs`

- [ ] **Step 1: Add failing asset tests**

Append this test to `HouseUpgradeDefinitionTests`:

```csharp
#if UNITY_EDITOR
[Test]
public void HOUSE_UPGRADE_040_FirstSliceAssetsExistAndAreValid()
{
    var stage = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset");
    var blueprint = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseConstructionBlueprintDefinition>("Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset");
    var condition = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeConditionBase>("Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset");
    var effect = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeEffectBase>("Assets/Data/Housing/Effects/HouseEffect_DirectConstructionReward.asset");

    Assert.IsNotNull(stage);
    Assert.IsNotNull(blueprint);
    Assert.IsNotNull(condition);
    Assert.IsNotNull(effect);
    Assert.AreEqual(1, stage.StageIndex);
    Assert.AreEqual(300, stage.HireCost);
    Assert.AreEqual(120, stage.DirectCost);
    Assert.AreSame(blueprint, stage.Blueprint);
    Assert.GreaterOrEqual(blueprint.RequiredCells.Count, 3);
}
#endif
```

- [ ] **Step 2: Run the asset test to verify it fails**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Housing","testClass":"HouseUpgradeDefinitionTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: FAIL because the four assets do not exist.

- [ ] **Step 3: Create asset factory**

Use `script-update-or-create` to create `Assets/Scripts/Game/Housing/HouseUpgradeAssetFactory.cs`:

```csharp
#if UNITY_EDITOR
using System.IO;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public static class HouseUpgradeAssetFactory
    {
        public static void CreateFirstSliceAssetsForEditor()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Housing");
            EnsureFolder("Assets/Data/Housing/UpgradeStages");
            EnsureFolder("Assets/Data/Housing/Blueprints");
            EnsureFolder("Assets/Data/Housing/Conditions");
            EnsureFolder("Assets/Data/Housing/Effects");

            var blueprint = LoadOrCreate<HouseConstructionBlueprintDefinition>("Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset");
            var condition = LoadOrCreate<HouseAlwaysCondition>("Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset");
            var effect = LoadOrCreate<HouseNoOpUpgradeEffect>("Assets/Data/Housing/Effects/HouseEffect_DirectConstructionReward.asset");
            var stage = LoadOrCreate<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset");

            var profile = InteriorGenerationProfile.CreateExpandedOfficeForTests();
            stage.ConfigureForTests("house.stage.expanded_room.01", 1, 300, 120, profile, null, blueprint);
            stage.ConfigureConditionsForTests(null, new HouseUpgradeConditionBase[] { condition });
            stage.ConfigureEffectsForTests(null, new HouseUpgradeEffectBase[] { effect });
            blueprint.ConfigureForTests(
                "house.blueprint.expanded_room.01",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            EditorUtility.SetDirty(stage);
            EditorUtility.SetDirty(blueprint);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(effect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    [CreateAssetMenu(fileName = "HouseEffect_NoOp", menuName = "Rootborn/Housing/Effects/No Op")]
    public sealed class HouseNoOpUpgradeEffect : HouseUpgradeEffectBase
    {
        public override bool CanApply(in HouseUpgradeContext context) => true;
        public override void Apply(in HouseUpgradeContext context) { }
    }
}
#endif
```

- [ ] **Step 4: Add configuration methods for asset creation**

Use `script-update-or-create` for `HouseUpgradeDefinitions.cs`. Add these methods:

```csharp
public void ConfigureForTests(string id, int stageIndex, int hireCost, int directCost, InteriorGenerationProfile profile, InteriorTileSetDefinition tileSet, HouseConstructionBlueprintDefinition blueprint)
{
    _id = id;
    _stageIndex = stageIndex;
    _hireCost = hireCost;
    _directCost = directCost;
    _profile = profile;
    _tileSet = tileSet;
    _blueprint = blueprint;
}
```

inside `HouseUpgradeStageDefinition`, and:

```csharp
public void ConfigureForTests(string id, RectInt bounds, HouseConstructionCellRequirement[] requiredCells)
{
    _id = id;
    _bounds = bounds;
    _requiredCells = requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();
}
```

inside `HouseConstructionBlueprintDefinition`.

- [ ] **Step 5: Execute asset factory through Unity MCP**

Run:

```powershell
$code = @'
using Rootborn.Game.Housing;
public static class HouseUpgradeAssetFactoryRunner
{
    public static void Run()
    {
        HouseUpgradeAssetFactory.CreateFirstSliceAssetsForEditor();
    }
}
'@
$json = @{ code = $code; className = "HouseUpgradeAssetFactoryRunner"; methodName = "Run" } | ConvertTo-Json
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool script-execute --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: four assets are created under `Assets/Data/Housing/`.

- [ ] **Step 6: Run asset tests**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Housing/HouseUpgradeDefinitions.cs Assets/Scripts/Game/Housing/HouseUpgradeAssetFactory.cs Assets/Scripts/Game/Housing/HouseUpgradeAssetFactory.cs.meta Assets/Data/Housing Assets/Tests/EditMode/Housing/HouseUpgradeDefinitionTests.cs
git commit -m "[ASSET][TEST] House 첫 확장 데이터 에셋 추가"
```

## Task 3: Hire Route Player Flow

**Files:**
- Modify: `Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`
- Modify: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode test for hire completion**

Append:

```csharp
[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_004_HireRoutePaysSavesStageAndReloadsExpandedHouse()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return null;
    yield return null;

    HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

    var panel = Object.FindFirstObjectByType<Rootborn.UI.Housing.HouseUpgradePanel>(FindObjectsInactive.Include);
    Assert.IsNotNull(panel);
    panel.OpenDefaultOfferForTests("slot-0", directEligible: false);
    yield return null;

    var hireButton = GameObject.Find("HireConstructionButton").GetComponent<Button>();
    hireButton.onClick.Invoke();
    yield return null;

    var saved = HouseStatePersistence.Load("slot-0");
    Assert.AreEqual(1, saved.CurrentStageIndex);
    Assert.AreEqual(HouseUpgradeRouteKind.HireConstruction, saved.LatestRoute);
    Assert.AreEqual(200, saved.Currency.Balance);

    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;
    Assert.Greater(CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>()), 120);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the PlayMode `HouseUpgradeFlowPlayModeTests` command from Task 1.

Expected: FAIL because `OpenDefaultOfferForTests` and hire persistence wiring do not exist.

- [ ] **Step 3: Wire default offer and hire completion**

Use `script-update-or-create` to update `HouseUpgradePanel.cs`. Add:

```csharp
public void OpenDefaultOfferForTests(string saveSlot, bool directEligible)
{
    var stage = UnityEngine.Resources.Load<HouseUpgradeStageDefinition>("HouseStage_ExpandedRoom_01");
    if (stage == null)
    {
        stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.expanded_room.01", 1, 300, 120, Rootborn.Game.Interiors.InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, null);
    }

    var state = HouseStatePersistence.Load(saveSlot);
    var wallet = new HouseCurrencyWallet(state.Currency != null ? state.Currency.Balance : 0);
    Show(stage, state, wallet, directEligible);
    HireRequested += (selectedStage, selectedState, selectedWallet) =>
    {
        var service = new HouseUpgradeService(new[] { selectedStage });
        var result = service.TryHire(selectedStage, selectedState, selectedWallet);
        if (result.Kind == HouseUpgradeResultKind.Applied)
        {
            selectedState.Currency.Balance = selectedWallet.Balance;
            HouseStatePersistence.Save(saveSlot, selectedState);
        }
    };
}
```

If asset loading through `Resources` is not suitable, replace the fallback with an explicit serialized/default provider in `HouseUpgradeRuntimeInstaller` and keep the test path deterministic.

- [ ] **Step 4: Run hire flow test**

Run the PlayMode `HouseUpgradeFlowPlayModeTests` command.

Expected: PASS through `HOUSE_UPGRADE_PM_004`.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/Housing/HouseUpgradePanel.cs Assets/Scripts/UI/Housing/HouseUpgradeRuntimeInstaller.cs Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs
git commit -m "[UI][TEST] House 업체 시공 저장 흐름 추가"
```

## Task 4: Direct Construction Overlay

**Files:**
- Create: `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`
- Modify: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing direct construction overlay test**

Append:

```csharp
[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_005_DirectRouteStartsConstructionOverlayAndTracksProgress()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
        "blueprint.direct",
        new RectInt(0, 0, 4, 4),
        new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

    var overlay = Rootborn.UI.Housing.HouseConstructionOverlay.EnsureForTests(blueprint);
    Assert.IsNotNull(overlay);
    Assert.AreEqual("0/3", overlay.ProgressTextForTests);
    Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
    Assert.AreEqual("1/3", overlay.ProgressTextForTests);
    Assert.IsFalse(overlay.CompleteButtonInteractableForTests);
}
```

- [ ] **Step 2: Run to verify failure**

Run the PlayMode `HouseUpgradeFlowPlayModeTests` command.

Expected: FAIL because `HouseConstructionOverlay` does not exist.

- [ ] **Step 3: Implement overlay**

Use `script-update-or-create` to create `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`:

```csharp
using Rootborn.Game.Housing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseConstructionOverlay : MonoBehaviour
    {
        private HouseConstructionBlueprintDefinition _blueprint;
        private HouseConstructionSession _session;
        private Text _progressText;
        private Button _completeButton;
        private Button _cancelButton;

        public string ProgressTextForTests => _progressText != null ? _progressText.text : string.Empty;
        public bool CompleteButtonInteractableForTests => _completeButton != null && _completeButton.interactable;

        public static HouseConstructionOverlay EnsureForTests(HouseConstructionBlueprintDefinition blueprint)
        {
            var overlay = FindFirstObjectByType<HouseConstructionOverlay>() ?? Create();
            overlay.Bind(blueprint);
            return overlay;
        }

        public void Bind(HouseConstructionBlueprintDefinition blueprint)
        {
            if (_progressText == null) Build();
            _blueprint = blueprint;
            _session = new HouseConstructionSession(blueprint);
            gameObject.SetActive(true);
            Refresh();
        }

        public bool TryPlaceForTests(Vector2Int cell, HouseConstructionCellKind kind)
        {
            bool placed = _session != null && _session.TryPlace(cell, kind);
            Refresh();
            return placed;
        }

        private static HouseConstructionOverlay Create()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HouseConstructionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var go = new GameObject("HouseConstructionOverlay", typeof(RectTransform), typeof(Image), typeof(HouseConstructionOverlay));
            go.transform.SetParent(canvas.transform, false);
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(420f, 96f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            return go.GetComponent<HouseConstructionOverlay>();
        }

        private void Build()
        {
            _progressText = MakeText("ConstructionProgressText", new Vector2(0f, 18f), new Vector2(380f, 36f));
            _completeButton = MakeButton("CompleteConstructionButton", "Complete", new Vector2(-90f, -26f));
            _cancelButton = MakeButton("CancelConstructionButton", "Cancel", new Vector2(90f, -26f));
            _cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void Refresh()
        {
            int placed = _session != null ? _session.ToSaveData().Length : 0;
            int required = _blueprint != null ? _blueprint.RequiredCells.Count : 0;
            _progressText.text = placed + "/" + required;
            _completeButton.interactable = _session != null && _session.IsComplete;
        }

        private Text MakeText(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button MakeButton(string name, string label, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150f, 34f);
            go.GetComponent<Image>().color = Color.white;
            var text = MakeText("Text", Vector2.zero, rect.sizeDelta);
            text.transform.SetParent(go.transform, false);
            text.text = label;
            text.color = Color.black;
            return go.GetComponent<Button>();
        }
    }
}
```

- [ ] **Step 4: Run overlay test**

Run the PlayMode command.

Expected: PASS through `HOUSE_UPGRADE_PM_005`.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs.meta Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs
git commit -m "[UI][TEST] House 직접 시공 오버레이 추가"
```

## Task 5: Direct Construction Completion and Persistence

**Files:**
- Modify: `Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs`
- Modify: `Assets/Scripts/UI/Housing/HouseUpgradePanel.cs`
- Modify: `Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs`

- [ ] **Step 1: Add failing direct completion test**

Append:

```csharp
[UnityTest]
public IEnumerator HOUSE_UPGRADE_PM_006_DirectConstructionCompletesSavesStageAndClearsProgress()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return null;
    yield return null;

    var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
        "blueprint.direct.complete",
        new RectInt(0, 0, 4, 4),
        new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
    var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.direct.complete", 1, 300, 120, Rootborn.Game.Interiors.InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
    stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
    HouseStatePersistence.Save("slot-0", new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

    var overlay = Rootborn.UI.Housing.HouseConstructionOverlay.EnsureForTests(blueprint);
    overlay.BindCompletionForTests("slot-0", stage);
    Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
    Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(1, 2), HouseConstructionCellKind.Wall));
    Assert.IsTrue(overlay.TryPlaceForTests(new Vector2Int(2, 1), HouseConstructionCellKind.Door));
    Assert.IsTrue(overlay.CompleteButtonInteractableForTests);
    overlay.CompleteForTests();
    yield return null;

    var saved = HouseStatePersistence.Load("slot-0");
    Assert.AreEqual(1, saved.CurrentStageIndex);
    Assert.AreEqual(380, saved.Currency.Balance);
    Assert.AreEqual(string.Empty, saved.ActiveConstructionStageId);
    Assert.AreEqual(0, saved.PlacedConstructionCells.Length);
    Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, saved.LatestRoute);
}
```

- [ ] **Step 2: Run test to verify failure**

Run the PlayMode command.

Expected: FAIL because `BindCompletionForTests` and `CompleteForTests` do not exist.

- [ ] **Step 3: Implement completion binding**

Update `HouseConstructionOverlay.cs`:

```csharp
private string _saveSlot;
private HouseUpgradeStageDefinition _stage;

public void BindCompletionForTests(string saveSlot, HouseUpgradeStageDefinition stage)
{
    _saveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
    _stage = stage;
    if (_stage != null && _stage.Blueprint != null)
    {
        Bind(_stage.Blueprint);
    }
}

public void CompleteForTests()
{
    Complete();
}

private void Complete()
{
    if (_stage == null || _session == null) return;
    var state = HouseStatePersistence.Load(_saveSlot);
    var wallet = new HouseCurrencyWallet(state.Currency != null ? state.Currency.Balance : 0);
    var service = new HouseUpgradeService(new[] { _stage });
    var result = service.TryCompleteDirect(_stage, state, wallet, _session);
    if (result.Kind != HouseUpgradeResultKind.Applied) return;
    state.Currency.Balance = wallet.Balance;
    HouseStatePersistence.Save(_saveSlot, state);
    gameObject.SetActive(false);
}
```

In `Build`, add:

```csharp
_completeButton.onClick.AddListener(Complete);
```

- [ ] **Step 4: Run direct completion test**

Run the PlayMode command.

Expected: PASS through `HOUSE_UPGRADE_PM_006`.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/Housing/HouseConstructionOverlay.cs Assets/Tests/PlayMode/Housing/HouseUpgradeFlowPlayModeTests.cs
git commit -m "[UI][TEST] House 직접 시공 완료 저장 추가"
```

## Task 6: Camera Drag Pan Quality Improvement

**Files:**
- Modify: `Assets/Scripts/UI/Interiors/HousePlacementCameraController.cs`
- Modify: `Assets/Tests/PlayMode/Interiors/HousePlacementCameraControlPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode test for mouse drag pan**

Append:

```csharp
[UnityTest]
public IEnumerator HousePlacementCamera_RightMouseDragPansCameraThroughPlayerInput()
{
    yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
    yield return WaitForHousePlacementCamera();

    var camera = Camera.main;
    Assert.IsNotNull(camera);
    camera.orthographicSize = 4f;
    camera.transform.position = new Vector3(0f, 0f, camera.transform.position.z);

    var mouse = InputSystem.AddDevice<Mouse>();
    try
    {
        var start = camera.transform.position;
        yield return DriveMouseDrag(mouse, new Vector2(600f, 400f), new Vector2(500f, 360f), 6);
        Assert.AreNotEqual(start, camera.transform.position, "Right mouse drag should pan the placement camera while zoomed in.");
    }
    finally
    {
        if (mouse.added)
        {
            InputSystem.RemoveDevice(mouse);
        }
    }
}
```

Add this mouse input driver to the test class:

```csharp
private static IEnumerator DriveMouseDrag(Mouse mouse, Vector2 from, Vector2 to, int frames)
{
    var driver = new GameObject("PlacementCameraMouseDragInputDriver").AddComponent<MouseCameraInputDriver>();
    driver.Configure(mouse, from, to, frames);
    for (int i = 0; i < frames + 3; i++) yield return null;
    if (driver != null) Object.Destroy(driver.gameObject);
}

[DefaultExecutionOrder(-10000)]
private sealed class MouseCameraInputDriver : MonoBehaviour
{
    private Mouse _mouse;
    private Vector2 _from;
    private Vector2 _to;
    private int _frames;
    private int _frame;

    public void Configure(Mouse mouse, Vector2 from, Vector2 to, int frames)
    {
        _mouse = mouse;
        _from = from;
        _to = to;
        _frames = Mathf.Max(1, frames);
    }

    private void Update()
    {
        if (_mouse == null || !_mouse.added)
        {
            Destroy(gameObject);
            return;
        }

        _mouse.MakeCurrent();
        if (_frame < _frames)
        {
            float t = _frames <= 1 ? 1f : _frame / (float)(_frames - 1);
            var position = Vector2.Lerp(_from, _to, t);
            InputSystem.QueueStateEvent(_mouse, new MouseState
            {
                position = position,
                buttons = 1u << (int)MouseButton.Right
            });
            InputSystem.Update();
            _frame++;
            return;
        }

        InputSystem.QueueStateEvent(_mouse, new MouseState { position = _to });
        InputSystem.Update();
        Destroy(gameObject);
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:

```powershell
$json = '{"testMode":"PlayMode","testMethod":"Rootborn.Tests.PlayMode.Interiors.HousePlacementCameraControlPlayModeTests.HousePlacementCamera_RightMouseDragPansCameraThroughPlayerInput","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: FAIL because drag pan is not implemented.

- [ ] **Step 3: Implement right/middle mouse drag pan**

Update `HousePlacementCameraController.cs`:

```csharp
[SerializeField] private float _mouseDragPanScale = 1f;
private bool _dragging;
private Vector2 _lastMousePosition;
```

In `Update`, after scroll zoom handling:

```csharp
bool dragPressed = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
var currentMousePosition = mouse.position.ReadValue();
if (dragPressed && !_dragging)
{
    _dragging = true;
    _lastMousePosition = currentMousePosition;
}
else if (dragPressed && _dragging)
{
    var pixelDelta = currentMousePosition - _lastMousePosition;
    _lastMousePosition = currentMousePosition;
    var worldUnitsPerPixel = (_camera.orthographicSize * 2f) / Mathf.Max(1f, _camera.pixelHeight);
    var dragDelta = new Vector3(-pixelDelta.x, -pixelDelta.y, 0f) * (worldUnitsPerPixel * _mouseDragPanScale);
    _camera.transform.position = ClampPosition(_camera.transform.position + dragDelta, _bounds, _camera.orthographicSize, _camera.aspect);
}
else
{
    _dragging = false;
}
```

- [ ] **Step 4: Run camera tests**

Run both:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Interiors","testClass":"HousePlacementCameraControlPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

If the class run times out, run each method individually.

Expected: PASS for keyboard pan/zoom and mouse drag pan.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/Interiors/HousePlacementCameraController.cs Assets/Tests/PlayMode/Interiors/HousePlacementCameraControlPlayModeTests.cs
git commit -m "[UI][TEST] House 배치 카메라 드래그 이동 추가"
```

## Task 7: Direct Visual Verification Gate

**Files:**
- Create: `docs/superpowers/audits/2026-05-17-house-upgrade-next-slice-verification.md`
- Create directory: `Builds/Logs/house-upgrade-next-slice/`

- [ ] **Step 1: Run focused automated tests**

Run:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Housing","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: PASS for Housing EditMode tests.

Run:

```powershell
$json = '{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Housing","testClass":"HouseUpgradeFlowPlayModeTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path
Remove-Item -LiteralPath $path -Force
```

Expected: PASS for House upgrade PlayMode tests.

- [ ] **Step 2: Run entity-id branching gate**

Run:

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Expected: `OK: no entity-id branching in system code.`

- [ ] **Step 3: Capture Game View screenshot after direct completion**

Use Unity MCP `screenshot-game-view` after reproducing the flow in PlayMode:

1. Load `Town`.
2. Open the House upgrade panel.
3. Confirm hire route only for non-interior state.
4. Confirm direct route for direct-eligible state.
5. Load `House`.
6. Start construction overlay.
7. Place required floor, wall, and door cells.
8. Complete construction.
9. Capture screenshot to `Builds/Logs/house-upgrade-next-slice/direct-construction-complete.png`.

If screenshot tool returns an inline image only, save the evidence path and describe that the image was inspected in the audit.

- [ ] **Step 4: Inspect runtime and saved state**

Use a Unity MCP `script-execute` probe that reports:

```csharp
using Rootborn.Game.Housing;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HouseUpgradeVerificationProbe
{
    public static string Run()
    {
        var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
        var walls = GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
        var doors = GameObject.Find("HouseDoorTilemap")?.GetComponent<Tilemap>();
        var collision = GameObject.Find("HouseCollisionTilemap")?.GetComponent<Tilemap>();
        var state = HouseStatePersistence.Load("slot-0");
        return $"floor={Count(floor)} walls={Count(walls)} doors={Count(doors)} collision={Count(collision)} stage={state.CurrentStageIndex} route={state.LatestRoute} active={state.ActiveConstructionStageId} placed={(state.PlacedConstructionCells != null ? state.PlacedConstructionCells.Length : -1)}";
    }

    private static int Count(Tilemap tilemap)
    {
        if (tilemap == null) return -1;
        int count = 0;
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(pos) != null) count++;
        }
        return count;
    }
}
```

Expected:

- `stage=1`
- `route=DirectConstruction`
- `active=` empty
- `placed=0`
- floor/wall/door/collision counts are positive

- [ ] **Step 5: Create verification audit**

Create `docs/superpowers/audits/2026-05-17-house-upgrade-next-slice-verification.md` with:

```markdown
# House Upgrade Next Slice Verification

## Automated Tests

- Housing EditMode: PASS/FAIL with command and summary.
- House upgrade PlayMode: PASS/FAIL with command and summary.
- Camera PlayMode: PASS/FAIL with command and summary.
- Entity-id branching gate: PASS/FAIL with output.

## Direct PlayMode Flow

- Town panel opened through runtime installer: PASS/FAIL.
- Non-interior route visibility: PASS/FAIL.
- Direct-eligible route visibility: PASS/FAIL.
- Direct construction overlay started in House: PASS/FAIL.
- Required cells placed through player-facing path: PASS/FAIL.
- Completion saved stage 1: PASS/FAIL.
- Reloaded House shows expanded layout: PASS/FAIL.

## Visual Evidence

- Direct construction screenshot: `Builds/Logs/house-upgrade-next-slice/direct-construction-complete.png`
- Reloaded expanded House screenshot: `Builds/Logs/house-upgrade-next-slice/reloaded-expanded-house.png`

## Runtime State Probe

Paste the probe output here.

## Blocked or Residual Risk

State "None" only if every item above passed. If anything was not run, list it explicitly.
```

- [ ] **Step 6: Commit verification audit**

```powershell
git add -- docs/superpowers/audits/2026-05-17-house-upgrade-next-slice-verification.md Builds/Logs/house-upgrade-next-slice
git commit -m "[DOCS][TEST] House 확장 후속 검증 결과 기록"
```

## Self-Review

Spec coverage:

- Upgrade panel: Task 1.
- Route visibility: Task 1 tests.
- First-slice data assets: Task 2.
- Hire flow: Task 3.
- Direct construction overlay: Task 4.
- Direct completion persistence: Task 5.
- Camera drag pan recommended improvement: Task 6.
- Automated and visual verification: Task 7.

Placeholder scan:

- The plan contains no placeholder markers.
- Every code-changing task includes concrete file paths, code snippets, commands, and expected results.

Type consistency:

- `HouseUpgradeStageDefinition`, `HouseConstructionBlueprintDefinition`, `HouseConstructionSession`, `HouseUpgradeService`, and `HouseStatePersistence` match existing committed types.
- New UI types are consistently under `Rootborn.UI.Housing`.
- Test helpers use existing `CreateForTests` APIs and add explicit configuration methods before using them.
