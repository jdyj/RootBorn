# Town Concept Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert ROOTBORN from a farm/farmer concept to a town-life concept, with Style2 Modern UI as the first visual baseline.

**Architecture:** First lock the audit and tests, then migrate the reusable Modern UI recipe layer from Style1 to Style2, then connect the default scene and user-facing text to town-life semantics. Existing data-driven SO systems are reused where possible, while farm/crop runtime paths are isolated instead of deleted blindly.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create`, Addressables, ScriptableObject data, existing ROOTBORN asmdef boundaries.

---

## File Structure

### New docs

- `docs/art/town-concept-domain-mapping.md`
  - Human-readable mapping from farm concepts to town-life concepts.
- `docs/art/town-concept-audit.md`
  - Farm residue audit, Style1 usage audit, Town scene status, and deferred items.
- `docs/art/modern-ui-style2-reconstruction-manifest.json`
  - Style2 version of the existing Modern UI reconstruction manifest.

### Runtime and editor files to modify through Unity MCP only

- `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`
  - Add explicit Style2 recipe sub-sprite constants and keep Style1 available for legacy references.
- `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`
  - Point first-scope panel recipes to Style2 keys.
- `Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs`
  - Remove `FarmHelp` from the town baseline and replace it with a town activity kind.
- `Assets/Scripts/Game/ModernSociety/ModernActivityDefinition.cs`
  - Keep the SO definition data-driven; only extend if audit shows a missing town field.
- `Assets/Scripts/Game/Bootstrap/FarmAutoFillerRuntimeInstaller.cs`
  - Isolate from the default Town boot path in this plan; rename in the explicit technical rename phase.
- `Assets/Scripts/Game/Bootstrap/FarmModernUiPanelBridge.cs`
  - Make default scene logic town-aware without global find calls; rename in the explicit technical rename phase.
- `Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs`
  - Either replace with a town HUD installer or document as legacy if not in default path.

### Tests

- `Assets/Tests/EditMode/TownConcept/TownConceptDomainMappingTests.cs`
- `Assets/Tests/EditMode/TownConcept/TownConceptSourceAuditTests.cs`
- `Assets/Tests/EditMode/TownConcept/ModernUiStyle2RecipeTests.cs`
- `Assets/Tests/EditMode/ModernSociety/ModernSocietyLoopTests.cs`
- `Assets/Tests/PlayMode/TownConcept/TownSceneBootTests.cs`
- `Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs`

---

### Task 1: Town Concept Audit Documents

**Files:**
- Create: `docs/art/town-concept-domain-mapping.md`
- Create: `docs/art/town-concept-audit.md`

- [ ] **Step 1: Write the domain mapping document**

Create `docs/art/town-concept-domain-mapping.md` with this content:

```markdown
# Town Concept Domain Mapping

Date: 2026-05-08

## Direction

ROOTBORN now targets a town-life fantasy. The player is a city resident, not a farmer.

## Mapping

| Farm concept | Town-life replacement | First implementation action |
| --- | --- | --- |
| Farm scene | Town scene / district scene | Use `Assets/Scenes/Town.unity` as the default playable direction after audit. |
| Farmer | City resident | Update user-facing labels and status text before technical renames. |
| Crop | Activity progress / personal task / work shift | Remove crop growth from the first user-facing loop. |
| Farm tool | Daily-life tool / phone / transit card / work badge / notebook | Reuse `ToolDefinition` only as data, not as farm semantics. |
| Resource node | Shop, service counter, errand point, neighborhood object | Remove tree/rock gathering from the default town path. |
| Recipe | Preparation, purchase, service use, work output | Keep data-driven result logic. |
| Quest | Neighbor request, job task, city event, relationship event | Preserve reward transaction preflight and idempotency. |
| Knowledge | District info, job skill, relationship tip, life tip | Keep `KnowledgeNode` data-driven. |
| Status | Energy, money, anxiety, knowledge, relationships | Use `ModernSocietyStats` as the first town stat vocabulary. |
| Inventory | Bag / possessions | Preserve capacity and duplicate handling. |

## First-Scope User-Facing Terms

Replace or hide these terms in first-scope UI: `Farm`, `Farmer`, `Crop`, `Harvest`, `Water Crop`, `Fertilize Crop`, `Farm Help`.

Allowed legacy terms only in source symbols or audit notes: `Farm`, `Crop`, `Farming`.
```

- [ ] **Step 2: Write the audit document**

Create `docs/art/town-concept-audit.md` with this content:

```markdown
# Town Concept Audit

Date: 2026-05-08

## Farm Residue Classes

| Class | Examples | First action |
| --- | --- | --- |
| User-facing text | HUD labels, quest text, status text | Replace with town-life labels. |
| Scene path | `Assets/Scenes/Farm.unity` | Move default playable flow to `Assets/Scenes/Town.unity` after smoke test. |
| Runtime type name | `FarmAutoFillerRuntimeInstaller`, `SeededFarmWorldApplier` | Isolate from default path first; rename in the explicit technical rename phase. |
| Data asset | `Crop_Wheat.asset`, `Effect_WaterCrop.asset` | Remove from first-scope registry or mark legacy. |
| Tests | `Farm` or `Crop` scenario names | Keep only when testing legacy isolation. |

## Style1 To Style2 UI Decision

The first town UI baseline uses `sprites/ui/modern/16/style-2`.

Style1 remains available only for legacy comparison and migration tests.

## Deferred Technical Renames

Technical renames are deferred until the default town path is green. This avoids broad scene, prefab, asmdef, and serialized reference churn before behavior is verified.
```

- [ ] **Step 3: Check for accidental placeholders**

Run:

```powershell
rg "placeholder" docs/art/town-concept-domain-mapping.md docs/art/town-concept-audit.md
```

Expected: no matches.

- [ ] **Step 4: Commit the docs**

Run:

```powershell
git add -- docs/art/town-concept-domain-mapping.md docs/art/town-concept-audit.md
git commit -m "[DOCS] town 컨셉 전환 감사 문서 추가"
```

Expected: commit succeeds with only the two new docs staged.

---

### Task 2: Style2 Manifest And Tests

**Files:**
- Create: `docs/art/modern-ui-style2-reconstruction-manifest.json`
- Create: `Assets/Tests/EditMode/TownConcept/ModernUiStyle2RecipeTests.cs`
- Modify: `Assets/Tests/EditMode/Rootborn.Tests.EditMode.asmdef` if the new folder is not automatically included by the existing test assembly.

- [ ] **Step 1: Create the Style2 manifest**

Create `docs/art/modern-ui-style2-reconstruction-manifest.json` by copying the current role set from `docs/art/modern-ui-reconstruction-manifest.json`, then replacing every first-scope sprite entry:

```json
{
  "style": "style-2",
  "sourceAddress": "sprites/ui/modern/16/style-2",
  "sourceAsset": "Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png",
  "mappingRule": "Style1 row/column maps to Style2 row/column when the coordinate exists.",
  "sprites": [
    { "id": "panel.tl", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r0_c0", "row": 0, "column": 0, "role": "corner-tl", "usage": "common panel top-left corner" },
    { "id": "panel.t", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r0_c1", "row": 0, "column": 1, "role": "edge-t", "usage": "common panel top edge" },
    { "id": "panel.tr", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r0_c2", "row": 0, "column": 2, "role": "corner-tr", "usage": "common panel top-right corner" },
    { "id": "panel.l", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r1_c0", "row": 1, "column": 0, "role": "edge-l", "usage": "common panel left edge" },
    { "id": "panel.fill", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r1_c1", "row": 1, "column": 1, "role": "fill", "usage": "common panel fill" },
    { "id": "panel.r", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r1_c2", "row": 1, "column": 2, "role": "edge-r", "usage": "common panel right edge" },
    { "id": "panel.bl", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r2_c0", "row": 2, "column": 0, "role": "corner-bl", "usage": "common panel bottom-left corner" },
    { "id": "panel.b", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r2_c1", "row": 2, "column": 1, "role": "edge-b", "usage": "common panel bottom edge" },
    { "id": "panel.br", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r2_c2", "row": 2, "column": 2, "role": "corner-br", "usage": "common panel bottom-right corner" },
    { "id": "button.base", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r1_c7", "row": 1, "column": 7, "role": "button", "usage": "settings and inventory command button face" },
    { "id": "button.close", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r1_c9", "row": 1, "column": 9, "role": "close-button", "usage": "settings close button" },
    { "id": "slot.base", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r3_c0", "row": 3, "column": 0, "role": "slot", "usage": "inventory item slot frame" },
    { "id": "tab.base", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r2_c8", "row": 2, "column": 8, "role": "tab", "usage": "top title tab and category tab" },
    { "id": "gauge.fill", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r4_c3", "row": 4, "column": 3, "role": "gauge-fill", "usage": "status gauge fill tile" },
    { "id": "scrollbar.thumb", "sheetAddress": "sprites/ui/modern/16/style-2", "subSpriteName": "ModernUI_16_Style2_r2_c3", "row": 2, "column": 3, "role": "scrollbar", "usage": "inventory scrollbar thumb" }
  ],
  "recipes": {
    "commonPanel": ["panel.tl", "panel.t", "panel.tr", "panel.l", "panel.fill", "panel.r", "panel.bl", "panel.b", "panel.br"],
    "inventory": ["commonPanel", "title-tabs", "slot-grid", "scrollbar", "selection-cursor", "popup"],
    "status": ["commonPanel", "title-tabs", "portrait-frame", "gauge-row", "icon-frame", "bottom-buttons"],
    "quest": ["commonPanel", "quest-list", "quest-detail", "objective-progress", "reward-row", "claim-button", "scrollbar"],
    "popup": ["commonPanel", "item-icon", "item-name", "item-description", "item-count", "action-buttons"]
  }
}
```

If an existing first-scope Style1 coordinate is missing from Style2, do not invent a coordinate. Add it to `docs/art/town-concept-audit.md` under a `Style2 Missing Coordinates` section and choose a concrete replacement coordinate in the manifest with a `replacementFor` field.

- [ ] **Step 2: Write the failing Style2 manifest test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/TownConcept/ModernUiStyle2RecipeTests.cs` with:

```csharp
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class ModernUiStyle2RecipeTests
    {
        private const string ManifestPath = "docs/art/modern-ui-style2-reconstruction-manifest.json";

        [Test]
        public void Style2Manifest_UsesStyle2AddressAndSpriteNames()
        {
            string json = File.ReadAllText(ManifestPath);

            StringAssert.Contains("\"sourceAddress\": \"" + ModernUISpriteAddresses.Style16Alt + "\"", json);
            StringAssert.DoesNotContain("sprites/ui/modern/16/style-1", json);
            StringAssert.DoesNotContain("ModernUI_16_Style1", json);
            StringAssert.Contains("ModernUI_16_Style2_r0_c0", json);
        }

        [Test]
        public void RuntimeCommonPanel_UsesStyle2SpritesForTownBaseline()
        {
            foreach (ModernUiSpriteKey tile in ModernUiRecipes.CommonPanel.Tiles)
            {
                Assert.AreEqual(ModernUISpriteAddresses.Style16Alt, tile.SheetAddress);
                StringAssert.StartsWith("ModernUI_16_Style2_", tile.SubSpriteName);
            }
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails before implementation**

Run:

```powershell
# Use Unity Test Runner MCP if the editor is available:
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.ModernUiStyle2RecipeTests
```

Expected before implementation: `RuntimeCommonPanel_UsesStyle2SpritesForTownBaseline` fails because `ModernUiRecipes.CommonPanel` still uses `Style16` and `ModernUI_16_Style1_*`.

- [ ] **Step 4: Implement the minimal Style2 runtime recipe switch**

Use Unity MCP `script-update-or-create` to update `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`. Change only `CommonPanel` first:

```csharp
public static readonly ModernUiTileRecipe CommonPanel = new ModernUiTileRecipe(new[]
{
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r0_c0", 0, 0, "corner-tl"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r0_c1", 0, 1, "edge-t"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r0_c2", 0, 2, "corner-tr"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c0", 1, 0, "edge-l"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c1", 1, 1, "fill"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c2", 1, 2, "edge-r"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c0", 2, 0, "corner-bl"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c1", 2, 1, "edge-b"),
    new ModernUiSpriteKey(ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c2", 2, 2, "corner-br"),
});
```

- [ ] **Step 5: Run the Style2 tests**

Run:

```powershell
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.ModernUiStyle2RecipeTests
```

Expected: both tests pass.

- [ ] **Step 6: Commit the Style2 baseline**

Run:

```powershell
git add -- docs/art/modern-ui-style2-reconstruction-manifest.json Assets/Tests/EditMode/TownConcept/ModernUiStyle2RecipeTests.cs Assets/Scripts/UI/Modern/ModernUiRecipes.cs
git commit -m "[UI][TEST] town 기준 Style2 UI 레시피 전환"
```

---

### Task 3: Modern Society Farm Terminology Removal

**Files:**
- Modify: `Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs`
- Modify: `Assets/Tests/EditMode/ModernSociety/ModernSocietyLoopTests.cs`
- Create: `Assets/Tests/EditMode/TownConcept/TownConceptSourceAuditTests.cs`

- [ ] **Step 1: Write the failing source audit test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/TownConcept/TownConceptSourceAuditTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptSourceAuditTests
    {
        [Test]
        public void ModernSocietyActivityKinds_DoNotExposeFarmHelp()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs");
            StringAssert.DoesNotContain("FarmHelp", source);
        }

        [Test]
        public void TownConceptDocs_RecordFarmResidueAudit()
        {
            Assert.IsTrue(File.Exists("docs/art/town-concept-audit.md"));
            string audit = File.ReadAllText("docs/art/town-concept-audit.md");
            StringAssert.Contains("Farm Residue Classes", audit);
            StringAssert.Contains("Style1 To Style2 UI Decision", audit);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownConceptSourceAuditTests
```

Expected before implementation: `ModernSocietyActivityKinds_DoNotExposeFarmHelp` fails.

- [ ] **Step 3: Replace the activity kind**

Use Unity MCP `script-update-or-create` to update `Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs`:

```csharp
namespace Rootborn.Game.ModernSociety
{
    public enum ModernActivityKind
    {
        Study,
        Errand,
        Work,
        Rest,
        Socialize
    }
}
```

- [ ] **Step 4: Update the existing loop test**

Use Unity MCP `script-update-or-create` to update the specific test in `Assets/Tests/EditMode/ModernSociety/ModernSocietyLoopTests.cs`.

Replace any `FarmHelp` scenario with:

```csharp
[Test]
public void MOD_SCH_002_StudyAndErrandInSameDay_SetsBalancedRoutineFlag()
{
    var study = MakeActivity(ModernActivityKind.Study, energyCost: 1, knowledgeDelta: 1);
    var errand = MakeActivity(ModernActivityKind.Errand, energyCost: 1, moneyDelta: 1);
    var rule = ScriptableObject.CreateInstance<ModernRoutineFlagRule>();

    SetRequiredActivities(rule, study, errand);

    var day = new ModernSocietyDay(new ModernSocietyStats(energy: 4, money: 0, anxiety: 0, knowledge: 0, relationships: 0));
    Assert.IsTrue(day.TryPerform(study));
    Assert.IsTrue(day.TryPerform(errand));

    Assert.IsTrue(rule.IsSatisfied(day.PerformedActivities));
}
```

Keep the helper methods already present in the file. Do not introduce entity ID branches.

- [ ] **Step 5: Run ModernSociety and source audit tests**

Run:

```powershell
# tests-run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.ModernSociety
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownConceptSourceAuditTests
```

Expected: all pass.

- [ ] **Step 6: Commit the terminology removal**

Run:

```powershell
git add -- Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs Assets/Tests/EditMode/ModernSociety/ModernSocietyLoopTests.cs Assets/Tests/EditMode/TownConcept/TownConceptSourceAuditTests.cs
git commit -m "[REFACTOR][TEST] ModernSociety 농장 활동 용어 제거"
```

---

### Task 4: Town Scene And Default Flow Audit Tests

**Files:**
- Create: `Assets/Tests/EditMode/TownConcept/TownConceptDomainMappingTests.cs`
- Create: `Assets/Tests/PlayMode/TownConcept/TownSceneBootTests.cs`
- Modify: `ProjectSettings/EditorBuildSettings.asset` only through Unity Editor APIs if scene build order must change.

- [ ] **Step 1: Write the domain mapping test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/TownConcept/TownConceptDomainMappingTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptDomainMappingTests
    {
        [Test]
        public void DomainMapping_DocumentsTownReplacementForFarmLoop()
        {
            string mapping = File.ReadAllText("docs/art/town-concept-domain-mapping.md");

            StringAssert.Contains("City resident", mapping);
            StringAssert.Contains("Town scene", mapping);
            StringAssert.Contains("Activity progress", mapping);
            StringAssert.Contains("Neighbor request", mapping);
            StringAssert.DoesNotContain("Use farming as first loop", mapping);
        }

        [Test]
        public void TownSceneAsset_Exists()
        {
            Assert.IsTrue(File.Exists("Assets/Scenes/Town.unity"), "Town scene must exist before default flow migration.");
        }
    }
}
```

- [ ] **Step 2: Write the PlayMode boot smoke test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/PlayMode/TownConcept/TownSceneBootTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownSceneBootTests
    {
        [UnityTest]
        public IEnumerator TownScene_LoadsAsPlayableTownBaseline()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            Scene active = SceneManager.GetActiveScene();
            Assert.AreEqual("Town", active.name);
            Assert.IsTrue(active.rootCount > 0, "Town scene should have root objects.");
        }
    }
}
```

- [ ] **Step 3: Run tests to establish current state**

Run:

```powershell
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.TownConcept.TownConceptDomainMappingTests
# tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownConcept.TownSceneBootTests
```

Expected: EditMode passes if docs exist; PlayMode may fail if `Town` is not in build settings or scene has setup issues.

- [ ] **Step 4: If PlayMode fails because Town is missing from build settings, add it through Unity Editor APIs**

Use an Editor script or existing build settings tool through Unity MCP. Do not manually edit `ProjectSettings/EditorBuildSettings.asset`.

Expected build scene order:

1. `Assets/Scenes/Boot.unity`
2. `Assets/Scenes/MainMenu.unity`
3. `Assets/Scenes/HostLobby.unity`
4. `Assets/Scenes/Town.unity`
5. `Assets/Scenes/Farm.unity` as legacy only if still needed by tests

- [ ] **Step 5: Re-run PlayMode boot smoke**

Run:

```powershell
# tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownConcept.TownSceneBootTests
```

Expected: pass.

- [ ] **Step 6: Commit scene flow audit**

Run:

```powershell
git add -- Assets/Tests/EditMode/TownConcept/TownConceptDomainMappingTests.cs Assets/Tests/PlayMode/TownConcept/TownSceneBootTests.cs ProjectSettings/EditorBuildSettings.asset
git commit -m "[TEST][UI] town 씬 기본 진입 검증 추가"
```

If `ProjectSettings/EditorBuildSettings.asset` did not change, omit it from `git add`.

---

### Task 5: Town Style2 Visual Smoke

**Files:**
- Create: `Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs`
- Modify: `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs` only if panels still assume Farm scene names.
- Modify: `Assets/Scripts/Game/Bootstrap/FarmModernUiPanelBridge.cs` only if default Town path cannot attach Style2 UI without it.
- Update: `docs/art/town-concept-audit.md`

- [ ] **Step 1: Write the PlayMode Style2 UI smoke test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownStyle2UiSmokeTests
    {
        [UnityTest]
        public IEnumerator TownScene_ModernUiCommonPanelUsesStyle2()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            foreach (ModernUiSpriteKey tile in ModernUiRecipes.CommonPanel.Tiles)
            {
                Assert.AreEqual(ModernUISpriteAddresses.Style16Alt, tile.SheetAddress);
                StringAssert.StartsWith("ModernUI_16_Style2_", tile.SubSpriteName);
            }
        }

        [UnityTest]
        public IEnumerator TownScene_HasNonEmptyVisualRoots()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.Greater(roots.Length, 0);

            bool hasRendererOrCanvas = false;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<Renderer>(true) != null ||
                    roots[i].GetComponentInChildren<Canvas>(true) != null)
                {
                    hasRendererOrCanvas = true;
                    break;
                }
            }

            Assert.IsTrue(hasRendererOrCanvas, "Town scene should expose visible world or UI roots.");
        }
    }
}
```

- [ ] **Step 2: Run smoke tests**

Run:

```powershell
# tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownConcept.TownStyle2UiSmokeTests
```

Expected before integration: first test passes after Task 2; second test may fail if `Town.unity` is empty.

- [ ] **Step 3: If `Town.unity` is empty, create or repair the scene through Unity tools**

Use Unity MCP scene/gameobject tools or existing scene builder tooling. The minimum first-scope scene needs:

- Main Camera
- EventSystem if UI is present
- Canvas with Style2-capable Modern UI panels or HUD installer
- At least two visual town-life hints: home/apartment interior, street/shop/service/community area

Do not hand-edit `.unity` YAML.

- [ ] **Step 4: Update the audit with visual evidence**

Append to `docs/art/town-concept-audit.md`:

```markdown
## Town Visual Smoke

- Town scene loaded in PlayMode.
- Visual roots were present.
- Modern UI common panel recipe uses `sprites/ui/modern/16/style-2`.
- Screenshot artifact: record the path produced by the PlayMode or MCP screenshot capture.
```

- [ ] **Step 5: Re-run smoke tests and capture screenshot**

Run:

```powershell
# tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownConcept.TownStyle2UiSmokeTests
# Use MCP screenshot-game-view or screenshot-camera after loading Town.
```

Expected: PlayMode tests pass and a screenshot artifact path is recorded in the audit.

- [ ] **Step 6: Commit visual smoke**

Run:

```powershell
git add -- Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs docs/art/town-concept-audit.md Assets/Scenes/Town.unity Assets/Scenes/Town.unity.meta
git commit -m "[UI][TEST] town Style2 화면 스모크 검증 추가"
```

If the scene did not change, omit `Assets/Scenes/Town.unity` and its `.meta`.

---

### Task 6: Final Gates And Completion Audit

**Files:**
- Update: `docs/art/town-concept-audit.md`

- [ ] **Step 1: Run focused EditMode tests**

Run:

```powershell
# tests-run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.TownConcept
# tests-run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.ModernSociety
```

Expected: all pass.

- [ ] **Step 2: Run focused PlayMode tests**

Run:

```powershell
# tests-run testMode=PlayMode testNamespace=Rootborn.Tests.PlayMode.TownConcept
```

Expected: all pass or any Unity Test Framework instability is documented with direct evidence and a manual MCP fallback screenshot.

- [ ] **Step 3: Run entity branching CI gate**

Run:

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Expected:

```text
OK: no entity-id branching in system code.
```

- [ ] **Step 4: Run source audit for forbidden APIs**

Run:

```powershell
rg "Resources\.Load|GameObject\.Find|FindObjectOfType|FindObjectsOfType" Assets/Scripts
```

Expected: no new matches from town conversion files. Existing legacy matches must be listed in `docs/art/town-concept-audit.md`.

- [ ] **Step 5: Update final audit evidence**

Append to `docs/art/town-concept-audit.md`:

```markdown
## Final Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| TownConcept EditMode | PASS | Record test runner output summary. |
| ModernSociety EditMode | PASS | Record test runner output summary. |
| TownConcept PlayMode | PASS | Record test runner output summary or documented runner instability plus manual screenshot evidence. |
| Entity ID branching gate | PASS | `OK: no entity-id branching in system code.` |
| Forbidden runtime lookup audit | PASS | No new town conversion matches. |

## Remaining Farm Residue

List each remaining `Farm` or `Crop` symbol and classify it as `legacy scene`, `technical rename deferred`, `data migration deferred`, or `must fix before completion`.
```

- [ ] **Step 6: Commit final audit**

Run:

```powershell
git add -- docs/art/town-concept-audit.md
git commit -m "[DOCS][TEST] town 전환 검증 결과 기록"
```

Expected: commit succeeds.

---

## Completion Checklist

- [ ] `docs/art/town-concept-domain-mapping.md` exists and maps farm concepts to town-life replacements.
- [ ] `docs/art/town-concept-audit.md` classifies farm residue and records verification evidence.
- [ ] `docs/art/modern-ui-style2-reconstruction-manifest.json` exists and contains no Style1 address or Style1 sub-sprite name.
- [ ] `ModernUiRecipes.CommonPanel` uses `ModernUISpriteAddresses.Style16Alt` and `ModernUI_16_Style2_*`.
- [ ] `ModernActivityKind` no longer exposes `FarmHelp`.
- [ ] `Town.unity` loads in PlayMode.
- [ ] A PlayMode smoke test confirms visible Town roots.
- [ ] A test confirms Style2 UI recipe usage.
- [ ] Entity ID branching CI gate passes.
- [ ] Forbidden runtime lookup audit has no new town conversion violations.
- [ ] Final report lists remaining farm residue and next priority.
