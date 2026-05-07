# Modern UI Sprite Reconstruction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the settings, inventory, and status UI from named 16x16 Modern UI sub-sprites instead of stretching single sprites or using placeholder colors.

**Architecture:** Keep the sliced sheet and Addressables work already present. Add a tested manifest/recipe layer that names every source cell, then add runtime UI builders that tile 16x16 corner/edge/fill sprites into panels, buttons, slots, tabs, and gauges. Replace the first-scope UI surfaces through the shared builders and prove the result with EditMode source/manifest tests plus PlayMode screenshot audits.

**Tech Stack:** Unity 6000.3.13f1, UGUI, Addressables cache via existing `Managers.Resource`, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for all `Assets/**/*.cs` writes.

---

## Current Evidence

- Reference GIFs downloaded to `docs/art/reference-modern-ui/`:
  - `settings.gif`: 352x272, 75 frames
  - `inventory.gif`: 358x348, 123 frames
  - `status.gif`: 352x288, 75 frames
- Extracted frames:
  - `settings-frame37.png`
  - `inventory-frame61.png`
  - `status-frame37.png`
- Existing Modern UI base:
  - `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs`
  - `Assets/Scripts/Editor/Tools/ModernUiAddressablesSetup.cs`
  - `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`
  - `Assets/Scripts/Game/Common/ModernHudSpriteKeys.cs`
- Existing risk:
  - `Assets/Scripts/UI/HUD/StatusHud.cs` still uses `Image.Type.Sliced` on single sub-sprites and color fallbacks.
  - `SaveSlotSelectPanel.cs` and `QuestHudAutoFiller.cs` still use direct color panels; they are audit/follow-up scope unless the first-scope UI depends on them.

---

## File Structure

- Create `docs/art/modern-ui-reconstruction-manifest.json`
  - Human-readable recipe manifest for common tiles, settings window, inventory window, and status window.
  - Stores `sheetAddress`, `subSpriteName`, `row`, `column`, `role`, and `usage`.
- Create `docs/art/modern-ui-reconstruction-audit.md`
  - Records replaced, deferred, and forbidden oversized usages.
- Create `Assets/Scripts/Game/Common/ModernUiSpriteKey.cs`
  - Small immutable key for `sheetAddress + subSpriteName + row + column + role`.
- Create `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`
  - Runtime recipe registry for common panel, slot, button, tab, gauge, and the three target windows.
- Create `Assets/Scripts/UI/Modern/ModernUiSpriteResolver.cs`
  - Resolves a recipe sprite key through existing preloaded Addressables cache. No new runtime `Resources.Load`.
- Create `Assets/Scripts/UI/Modern/ModernUiTileImage.cs`
  - Builds deterministic child `Image` tiles in a `RectTransform`.
- Create `Assets/Scripts/UI/Modern/ModernUiWindowBuilder.cs`
  - Shared helper for panel/title/tabs/buttons/slot grid/gauge rows.
- Create `Assets/Scripts/UI/Modern/SettingsPanel.cs`
  - Minimal open/closeable settings UI using the recipe layer.
- Modify `Assets/Scripts/UI/HUD/StatusHud.cs`
  - Replace first-scope inventory/status panel construction with `ModernUiWindowBuilder` and remove single-cell stretched panel usage in the first-scope UI.
- Modify `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`
  - Add every manifest-referenced 16x16 sub-sprite to preload declarations.
- Test `Assets/Tests/EditMode/ModernUiRecipeManifestTests.cs`
- Test `Assets/Tests/EditMode/ModernUiSourceAuditTests.cs`
- Test `Assets/Tests/EditMode/ModernUiTileImageTests.cs`
- Test `Assets/Tests/PlayMode/ModernUiReconstructionScenarioTests.cs`

---

### Task 1: Manifest And Sliced Sprite Coverage

**Files:**
- Create: `docs/art/modern-ui-reconstruction-manifest.json`
- Create: `Assets/Tests/EditMode/ModernUiRecipeManifestTests.cs`
- Modify: `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`

- [ ] **Step 1: Write the failing manifest test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/ModernUiRecipeManifestTests.cs`.

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiRecipeManifestTests
    {
        private const string ManifestPath = "docs/art/modern-ui-reconstruction-manifest.json";

        [Test]
        public void Manifest_ExistsForTargetWindows()
        {
            Assert.IsTrue(File.Exists(ManifestPath), "Missing Modern UI reconstruction manifest.");
            string json = File.ReadAllText(ManifestPath);
            StringAssert.Contains("\"settings\"", json);
            StringAssert.Contains("\"inventory\"", json);
            StringAssert.Contains("\"status\"", json);
        }

        [Test]
        public void Manifest_ReferencesExistingSlicedSubSprites()
        {
            var manifest = ModernUiRecipeManifestLoader.Load(ManifestPath);
            var entriesByAddress = ModernUiAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);
            var missing = new List<string>();

            foreach (var sprite in manifest.Sprites)
            {
                Assert.IsTrue(entriesByAddress.TryGetValue(sprite.sheetAddress, out string path), sprite.usage);
                var names = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Select(s => s.name).ToHashSet();
                if (!names.Contains(sprite.subSpriteName))
                {
                    missing.Add(sprite.usage + ":" + sprite.subSpriteName);
                }
            }

            Assert.IsEmpty(missing, "Manifest references missing sub-sprites: " + string.Join(", ", missing));
        }

        [Test]
        public void CommonPanelRecipe_HasNineDistinctTileRoles()
        {
            var manifest = ModernUiRecipeManifestLoader.Load(ManifestPath);
            CollectionAssert.AreEquivalent(
                new[] { "corner-tl", "edge-t", "corner-tr", "edge-l", "fill", "edge-r", "corner-bl", "edge-b", "corner-br" },
                manifest.CommonPanelRoles);
        }
    }
}
```

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiRecipeManifestTests
```

Expected: FAIL because `ModernUiRecipeManifestLoader` and the manifest do not exist.

- [ ] **Step 3: Add the manifest and minimal test loader**

Use `script-update-or-create` for the test file update only. The loader can be nested in the test file because production code should not parse docs JSON at runtime.

The manifest must include at least these common panel cells from `sprites/ui/modern/16/style-1`:

```json
{
  "sourceReferences": [
    {
      "name": "settings",
      "url": "https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk4NTA0OC5naWY=/original/ZFWQBO.gif",
      "localFrame": "docs/art/reference-modern-ui/settings-frame37.png",
      "size": "352x272"
    },
    {
      "name": "inventory",
      "url": "https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0OC5naWY=/original/dztsAE.gif",
      "localFrame": "docs/art/reference-modern-ui/inventory-frame61.png",
      "size": "358x348"
    },
    {
      "name": "status",
      "url": "https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0Ni5naWY=/original/Z9nBBy.gif",
      "localFrame": "docs/art/reference-modern-ui/status-frame37.png",
      "size": "352x288"
    }
  ],
  "sprites": [
    { "id": "panel.tl", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r0_c0", "row": 0, "column": 0, "role": "corner-tl", "usage": "common panel top-left corner" },
    { "id": "panel.t", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r0_c1", "row": 0, "column": 1, "role": "edge-t", "usage": "common panel top edge" },
    { "id": "panel.tr", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r0_c2", "row": 0, "column": 2, "role": "corner-tr", "usage": "common panel top-right corner" },
    { "id": "panel.l", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r1_c0", "row": 1, "column": 0, "role": "edge-l", "usage": "common panel left edge" },
    { "id": "panel.fill", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r1_c1", "row": 1, "column": 1, "role": "fill", "usage": "common panel fill" },
    { "id": "panel.r", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r1_c2", "row": 1, "column": 2, "role": "edge-r", "usage": "common panel right edge" },
    { "id": "panel.bl", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r2_c0", "row": 2, "column": 0, "role": "corner-bl", "usage": "common panel bottom-left corner" },
    { "id": "panel.b", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r2_c1", "row": 2, "column": 1, "role": "edge-b", "usage": "common panel bottom edge" },
    { "id": "panel.br", "sheetAddress": "sprites/ui/modern/16/style-1", "subSpriteName": "ModernUI_16_Style1_r2_c2", "row": 2, "column": 2, "role": "corner-br", "usage": "common panel bottom-right corner" }
  ],
  "recipes": {
    "commonPanel": ["panel.tl", "panel.t", "panel.tr", "panel.l", "panel.fill", "panel.r", "panel.bl", "panel.b", "panel.br"],
    "settings": ["commonPanel", "title-tabs", "close-button", "toggle-row", "slider-row"],
    "inventory": ["commonPanel", "title-tabs", "slot-grid", "scrollbar", "selection-cursor"],
    "status": ["commonPanel", "title-tabs", "portrait-frame", "gauge-row", "bottom-buttons"]
  }
}
```

- [ ] **Step 4: Add manifest sub-sprites to preload declarations**

Use Unity MCP `script-update-or-create` to update `ModernUISpriteAddresses.AllSheets` so every manifest sub-sprite is included in the `Style16` declaration.

- [ ] **Step 5: Run GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiRecipeManifestTests
```

Expected: PASS with all manifest sprites resolved.

---

### Task 2: Runtime Recipe Types And Sprite Resolver

**Files:**
- Create: `Assets/Scripts/Game/Common/ModernUiSpriteKey.cs`
- Create: `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`
- Create: `Assets/Scripts/UI/Modern/ModernUiSpriteResolver.cs`
- Create: `Assets/Tests/EditMode/ModernUiRecipeRuntimeTests.cs`

- [ ] **Step 1: Write failing runtime recipe tests**

Use Unity MCP `script-update-or-create` to create tests that assert:

```csharp
Assert.AreEqual(9, ModernUiRecipes.CommonPanel.Tiles.Count);
Assert.IsTrue(ModernUiRecipes.CommonPanel.Tiles.Any(t => t.Role == "corner-tl"));
Assert.IsTrue(ModernUiRecipes.InventoryWindow.RequiredParts.Contains("slot-grid"));
Assert.IsFalse(File.ReadAllText("Assets/Scripts/UI/Modern/ModernUiSpriteResolver.cs").Contains("Resources.Load"));
```

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiRecipeRuntimeTests
```

Expected: FAIL because runtime recipe files do not exist.

- [ ] **Step 3: Implement minimal recipe types**

Use Unity MCP `script-update-or-create`. Runtime recipes should be static immutable data for the first pass, using the same IDs as the docs manifest.

- [ ] **Step 4: Implement resolver**

Resolver API:

```csharp
public interface IModernUiSpriteResolver
{
    Sprite Resolve(ModernUiSpriteKey key);
}
```

Default implementation must call `Managers.Resource.GetCachedSubSprite(key.SheetAddress, key.SubSpriteName)` and return `null` if managers are not bootstrapped.

- [ ] **Step 5: Run GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiRecipeRuntimeTests
```

Expected: PASS.

---

### Task 3: Deterministic Tile Image Component

**Files:**
- Create: `Assets/Scripts/UI/Modern/ModernUiTileImage.cs`
- Create: `Assets/Tests/EditMode/ModernUiTileImageTests.cs`

- [ ] **Step 1: Write failing tile tests**

Use Unity MCP `script-update-or-create` to create tests that construct a `GameObject` with a `RectTransform` and `ModernUiTileImage`, assign `CommonPanel`, set size `160x96`, call `Rebuild()`, and assert:

```csharp
Assert.GreaterOrEqual(tileImage.TileCount, 30);
Assert.AreEqual(4, tileImage.CornerTileCount);
Assert.AreEqual(new Vector2(16f, 16f), tileImage.TileSize);
Assert.IsFalse(tileImage.HasStretchedCornerTiles);
```

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiTileImageTests
```

Expected: FAIL because `ModernUiTileImage` does not exist.

- [ ] **Step 3: Implement minimal tiler**

Use Unity MCP `script-update-or-create`. `ModernUiTileImage.Rebuild()` must:

- Destroy only its own generated children named `Tile_*`.
- Compute columns and rows using `Mathf.CeilToInt(rect.width / 16f)` and `Mathf.CeilToInt(rect.height / 16f)`.
- Place every child at a 16x16 `sizeDelta`.
- Choose corner, edge, or fill role from the recipe.
- Never set `Image.type = Image.Type.Sliced`.

- [ ] **Step 4: Run GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiTileImageTests
```

Expected: PASS.

---

### Task 4: Source Audit Gate

**Files:**
- Create: `Assets/Tests/EditMode/ModernUiSourceAuditTests.cs`
- Create: `docs/art/modern-ui-reconstruction-audit.md`

- [ ] **Step 1: Write failing audit tests**

Use Unity MCP `script-update-or-create` to assert first-scope files do not contain:

```csharp
StringAssert.DoesNotContain("Image.Type.Sliced", File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs"));
StringAssert.DoesNotContain("MakeDefaultSlotGO", File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs"));
StringAssert.DoesNotContain("new Color(0f, 0f, 0f, 0.4f)", File.ReadAllText("Assets/Scripts/UI/HUD/StatusHud.cs"));
```

Also assert `docs/art/modern-ui-reconstruction-audit.md` has `Replaced`, `Deferred`, and `Verification` sections.

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiSourceAuditTests
```

Expected: FAIL because first-scope UI still contains sliced single-sprite and fallback color paths.

- [ ] **Step 3: Create audit document**

Record:

- Replaced: `StatusHud` resource HUD, inventory panel, item slots, status/settings windows after tasks complete.
- Deferred: `SaveSlotSelectPanel`, `QuestHudAutoFiller`, `DialoguePanel` unless touched by first-scope UI.
- Verification: focused tests and screenshot audit names.

---

### Task 5: Replace Status And Inventory UI Construction

**Files:**
- Modify: `Assets/Scripts/UI/HUD/StatusHud.cs`
- Modify: `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`
- Test: `Assets/Tests/EditMode/ModernUiSourceAuditTests.cs`
- Test: existing `Assets/Tests/EditMode/InventoryUiTests.cs`

- [ ] **Step 1: Keep audit test RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiSourceAuditTests
```

Expected: FAIL until `StatusHud.cs` is switched off first-scope single-sprite stretched UI.

- [ ] **Step 2: Replace panels and slots**

Use Unity MCP `script-update-or-create` to edit `StatusHud.cs`. Replace `MakePanel`, `MakeSlotPrefab`, and `MakeSlotImageOnly` internals with `ModernUiTileImage`-backed child objects for panel/slot backgrounds. Keep inventory behavior unchanged.

- [ ] **Step 3: Replace selected/equipment box and buttons**

Use the same builder for icon frame, book buttons, ribbons, tabs, and gauges. Keep item selection, drag/drop, and equip click handlers unchanged.

- [ ] **Step 4: Run GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiSourceAuditTests
Unity MCP tests-run EditMode testClass=InventoryUiTests
```

Expected: PASS.

---

### Task 6: Settings Panel

**Files:**
- Create: `Assets/Scripts/UI/Modern/SettingsPanel.cs`
- Create: `Assets/Tests/PlayMode/ModernUiReconstructionScenarioTests.cs`

- [ ] **Step 1: Write failing PlayMode test**

Use Unity MCP `script-update-or-create` to create a PlayMode test that creates a Canvas, attaches `SettingsPanel`, calls `Show()`, asserts active tiled children exist, calls `Hide()`, and asserts inactive.

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run PlayMode testClass=ModernUiReconstructionScenarioTests
```

Expected: FAIL because `SettingsPanel` does not exist.

- [ ] **Step 3: Implement settings panel**

Use Unity MCP `script-update-or-create`. The panel must build:

- Root common panel.
- Title tab row.
- Close button.
- Four option rows: sound toggle, music toggle, text speed slider, fullscreen toggle.
- Public `Show()` and `Hide()`.

- [ ] **Step 4: Run GREEN**

Run:

```text
Unity MCP tests-run PlayMode testClass=ModernUiReconstructionScenarioTests
```

Expected: PASS.

---

### Task 7: Screenshot And Pixel Audit

**Files:**
- Modify: `Assets/Tests/PlayMode/ModernUiReconstructionScenarioTests.cs`
- Create generated screenshots under `Builds/Logs/modern-ui/`
- Modify: `docs/art/modern-ui-reconstruction-audit.md`

- [ ] **Step 1: Add failing screenshot audit**

The PlayMode test should open inventory/status/settings fixtures and save screenshots. It must assert:

```csharp
Assert.Greater(nonTransparentPixelCount, 1000);
Assert.Greater(panelTileCount, 30);
Assert.AreEqual(0, stretchedSixteenBySixteenPanelCount);
Assert.AreEqual(0, textOverflowCount);
```

- [ ] **Step 2: Run RED or identify missing fixture**

Run:

```text
Unity MCP tests-run PlayMode testClass=ModernUiReconstructionScenarioTests includeLogs=true
```

Expected: FAIL until screenshot capture and audit counters are implemented.

- [ ] **Step 3: Implement screenshot audit helpers**

Use Unity MCP `script-update-or-create`. The helper may use `ScreenCapture.CaptureScreenshot` in PlayMode and inspect UI hierarchy counts directly for tile/text checks.

- [ ] **Step 4: Run GREEN and focused regressions**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiRecipeManifestTests
Unity MCP tests-run EditMode testClass=ModernUiRecipeRuntimeTests
Unity MCP tests-run EditMode testClass=ModernUiTileImageTests
Unity MCP tests-run EditMode testClass=ModernUiSourceAuditTests
Unity MCP tests-run PlayMode testClass=ModernUiReconstructionScenarioTests includeLogs=true
```

Expected: all PASS.

---

### Task 8: Full Regression And Completion Audit

**Files:**
- Modify: `docs/art/modern-ui-reconstruction-audit.md`

- [ ] **Step 1: Run full EditMode**

Run:

```text
Unity MCP tests-run EditMode includeLogs=true
```

Expected: PASS.

- [ ] **Step 2: Run full PlayMode**

Run:

```text
Unity MCP tests-run PlayMode includeLogs=true
```

Expected: PASS.

- [ ] **Step 3: Run static gate**

Run:

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Expected: exit code 0.

- [ ] **Step 4: Completion audit**

Map every goal scenario to evidence:

- `UI-SPRITE-001`: `ModernUiSliceSetupTests`, `ModernUiRecipeManifestTests`.
- `UI-RECIPE-001`: manifest and runtime recipe tests.
- `UI-PANEL-001`: tile image tests and source audit tests.
- `UI-INVENTORY-001`: PlayMode inventory scenario screenshot and source audit.
- `UI-STATUS-001`: PlayMode status scenario screenshot and source audit.
- `UI-SETTINGS-001`: PlayMode settings scenario screenshot.
- `UI-VISUAL-001`: screenshot files and pixel/hierarchy audit assertions.
- `UI-AUDIT-001`: audit document.

- [ ] **Step 5: Final report**

Include:

- Reference image URLs and local frames.
- Sheet/sub-sprite coordinate list from manifest.
- Created/modified recipe, manifest, asset, and script files.
- Replaced UI list and deferred UI list.
- Screenshot verification results.
- Test commands and results.
- Remaining risks and next priority.
