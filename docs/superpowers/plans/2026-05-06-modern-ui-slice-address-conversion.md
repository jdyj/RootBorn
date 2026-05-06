# Modern UI Slice Address Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare the LimeZu Modern UI pack for ROOTBORN by cataloging its core sheets, slicing them deterministically, and creating the tested bridge needed to replace Pixelwood Fantasy Book UI addresses.

**Architecture:** Keep runtime UI addresses stable while adding a Modern UI sheet import layer under `Rootborn.Editor.Tools`. The first implementation unit only creates the catalog/slicer and tests; the next unit can safely move HUD sprite selection from Pixelwood single sprites to named Modern UI sub-sprites without guessing cell names.

**Tech Stack:** Unity 6000.3.13f1, NUnit EditMode tests, UnityEditor `TextureImporter`, Addressables setup code, LimeZu `Assets/modernuserinterface-win`.

---

## File Structure

- Create `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs`
  - Owns the Modern UI sheet catalog.
  - Slices the nine top-level Modern UI sheets into deterministic names:
    - `ModernUI_16_Style1_r{row}_c{col}`
    - `ModernUI_16_Style2_r{row}_c{col}`
    - `ModernUI_16_Gamepad_r{row}_c{col}`
    - equivalent `32` and `48` names.
  - Exposes test helpers for sheet paths, cell sizes, expected columns/rows, and sprite names.

- Create `Assets/Tests/EditMode/ModernUiSliceSetupTests.cs`
  - Verifies every catalog entry exists on disk.
  - Verifies every sheet dimension is divisible by its configured cell size.
  - Verifies the expected grid sizes match the actual PNG dimensions.
  - Verifies deterministic sprite naming.

- Later modify `Assets/Scripts/Editor/Tools/AddressablesSetup.cs`
  - Register Modern UI sheets as Addressables sheets after slicing.
  - Stop routing `UISpriteAddresses` to Pixelwood Fantasy Book paths.

- Later modify `Assets/Scripts/Game/Common/GameDataRegistry.cs`
  - Add Modern UI sheet/sub-sprite constants or replace current book-specific constants.

- Later modify `Assets/Scripts/UI/HUD/StatusHud.cs`
  - Resolve selected UI pieces through `SubSpr(sheet, subName)` for Modern UI cells.
  - Remove book/page-specific visual assumptions after the modern catalog is stable.

---

### Task 1: Modern UI Sheet Catalog And Slicer

**Files:**
- Create: `Assets/Tests/EditMode/ModernUiSliceSetupTests.cs`
- Create: `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs`

- [x] **Step 1: Write the failing catalog tests**

Create `Assets/Tests/EditMode/ModernUiSliceSetupTests.cs` through the Unity MCP `script-update-or-create` tool:

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
    public sealed class ModernUiSliceSetupTests
    {
        [Test]
        public void Catalog_ContainsNineModernUiSheets()
        {
            Assert.AreEqual(9, ModernUiSliceSetup.Targets.Count);
        }

        [Test]
        public void Catalog_PointsOnlyToModernUiPack()
        {
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                StringAssert.StartsWith("Assets/modernuserinterface-win/", target.AssetPath);
                Assert.IsFalse(target.AssetPath.Contains("Pixelwood"));
            }
        }

        [Test]
        public void Catalog_AssetsExistOnDisk()
        {
            var missing = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                if (!File.Exists(target.AssetPath))
                {
                    missing.Add(target.AssetPath);
                }
            }

            Assert.IsEmpty(missing, "Missing Modern UI sheet files: " + string.Join(", ", missing));
        }

        [Test]
        public void Catalog_DimensionsMatchCellSize()
        {
            var mismatches = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                var texture = LoadTexture(target.AssetPath);
                if (texture.width % target.CellSize != 0 || texture.height % target.CellSize != 0)
                {
                    mismatches.Add($"{target.AssetPath} {texture.width}x{texture.height} cell {target.CellSize}");
                }
            }

            Assert.IsEmpty(mismatches, "Modern UI sheets with non-grid dimensions: " + string.Join(", ", mismatches));
        }

        [Test]
        public void Catalog_ExpectedGridSizesMatchPngDimensions()
        {
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                var texture = LoadTexture(target.AssetPath);
                Assert.AreEqual(texture.width / target.CellSize, target.Columns, target.AssetPath);
                Assert.AreEqual(texture.height / target.CellSize, target.Rows, target.AssetPath);
            }
        }

        [Test]
        public void BuildSpriteName_UsesStableRowColumnConvention()
        {
            var target = ModernUiSliceSetup.Targets[0];
            Assert.AreEqual($"{target.LabelPrefix}_r2_c3", ModernUiSliceSetup.BuildSpriteName(target, 2, 3));
        }

        [Test]
        public void SlicedSheets_ExposeFirstAndLastNamedSubSprites()
        {
            var missing = new List<string>();
            foreach (var target in ModernUiSliceSetup.Targets)
            {
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(target.AssetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();

                string first = ModernUiSliceSetup.BuildSpriteName(target, 0, 0);
                string last = ModernUiSliceSetup.BuildSpriteName(target, target.Rows - 1, target.Columns - 1);
                if (!spriteNames.Contains(first))
                {
                    missing.Add($"{target.AssetPath}:{first}");
                }

                if (!spriteNames.Contains(last))
                {
                    missing.Add($"{target.AssetPath}:{last}");
                }
            }

            Assert.IsEmpty(missing, "Modern UI sheets not sliced with expected sub-sprite names: " + string.Join(", ", missing));
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(bytes), path);
            return texture;
        }
    }
}
```

- [x] **Step 2: Run test to verify RED**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiSliceSetupTests
```

Expected: FAIL because `ModernUiSliceSetup` does not exist.

- [x] **Step 3: Add the minimal Modern UI slicer**

Create `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs` through the Unity MCP `script-update-or-create` tool:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernUiSliceSetup
    {
        public readonly struct SliceTarget
        {
            public SliceTarget(string assetPath, int cellSize, string labelPrefix, int columns, int rows)
            {
                AssetPath = assetPath;
                CellSize = cellSize;
                LabelPrefix = labelPrefix;
                Columns = columns;
                Rows = rows;
            }

            public string AssetPath { get; }
            public int CellSize { get; }
            public string LabelPrefix { get; }
            public int Columns { get; }
            public int Rows { get; }
        }

        public static readonly IReadOnlyList<SliceTarget> Targets = new[]
        {
            new SliceTarget("Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png", 16, "ModernUI_16_Style1", 61, 43),
            new SliceTarget("Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png", 16, "ModernUI_16_Style2", 49, 34),
            new SliceTarget("Assets/modernuserinterface-win/16x16/Modern_UI_Gamepad.png", 16, "ModernUI_16_Gamepad", 51, 51),
            new SliceTarget("Assets/modernuserinterface-win/32x32/Modern_UI_Style_1_32x32.png", 32, "ModernUI_32_Style1", 61, 43),
            new SliceTarget("Assets/modernuserinterface-win/32x32/Modern_UI_Style_2_32x32.png", 32, "ModernUI_32_Style2", 49, 34),
            new SliceTarget("Assets/modernuserinterface-win/32x32/Modern_UI_Gamepad_32x32.png", 32, "ModernUI_32_Gamepad", 51, 51),
            new SliceTarget("Assets/modernuserinterface-win/48x48/Modern_UI_Style_1_48x48.png", 48, "ModernUI_48_Style1", 61, 43),
            new SliceTarget("Assets/modernuserinterface-win/48x48/Modern_UI_Style_2_48x48.png", 48, "ModernUI_48_Style2", 49, 34),
            new SliceTarget("Assets/modernuserinterface-win/48x48/Modern_UI_Gamepad_48x48.png", 48, "ModernUI_48_Gamepad", 51, 51),
        };

        [MenuItem("Rootborn/Modern UI/Slice Sprite Sheets")]
        public static void SliceAll()
        {
            int sliced = 0;
            for (int i = 0; i < Targets.Count; i++)
            {
                if (SliceOne(Targets[i]))
                {
                    sliced++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Modern UI slice complete. {sliced} sheets processed.");
        }

        public static string BuildSpriteName(SliceTarget target, int row, int column)
        {
            return $"{target.LabelPrefix}_r{row}_c{column}";
        }

        private static bool SliceOne(SliceTarget target)
        {
            var importer = AssetImporter.GetAtPath(target.AssetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ROOTBORN] not a TextureImporter: {target.AssetPath}");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = target.CellSize;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target.AssetPath);
            if (texture == null)
            {
                Debug.LogWarning($"[ROOTBORN] failed to load texture: {target.AssetPath}");
                return false;
            }

            int columns = texture.width / target.CellSize;
            int rows = texture.height / target.CellSize;
            if (columns != target.Columns || rows != target.Rows)
            {
                Debug.LogWarning($"[ROOTBORN] Modern UI grid mismatch: {target.AssetPath} expected {target.Columns}x{target.Rows}, actual {columns}x{rows}");
                return false;
            }

            var metas = new List<SpriteMetaData>(columns * rows);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int x = column * target.CellSize;
                    int yFromTop = row * target.CellSize;
                    int yFromBottom = texture.height - target.CellSize - yFromTop;
                    metas.Add(new SpriteMetaData
                    {
                        name = BuildSpriteName(target, row, column),
                        rect = new Rect(x, yFromBottom, target.CellSize, target.CellSize),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        border = Vector4.zero
                    });
                }
            }

#pragma warning disable CS0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[ROOTBORN] Sliced {target.AssetPath} -> {metas.Count} sprites ({columns}x{rows})");
            return true;
        }
    }
}
```

- [x] **Step 4: Run test to verify GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=ModernUiSliceSetupTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

Stage only these files:

```bash
git add docs/superpowers/plans/2026-05-06-modern-ui-slice-address-conversion.md Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs.meta Assets/Tests/EditMode/ModernUiSliceSetupTests.cs Assets/Tests/EditMode/ModernUiSliceSetupTests.cs.meta
git commit -m "[TOOL][TEST] 모던 UI 시트 슬라이스 도구 추가"
```

---

### Task 2: Wire Modern UI Sheets Into Addressables

**Files:**
- Modify: `Assets/Tests/EditMode/UISpriteAddressesTests.cs`
- Modify: `Assets/Scripts/Editor/Tools/AddressablesSetup.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`

- [ ] **Step 1: Add failing tests proving UI sources are no longer Pixelwood**

Add tests that assert every UI entry registered by `AddressablesSetup.GetUiSpriteEntries()` points under `Assets/modernuserinterface-win/` and none contains `Pixelwood`.

- [ ] **Step 2: Run RED**

Run:

```text
Unity MCP tests-run EditMode testClass=UISpriteAddressesTests
```

Expected: FAIL because current UI entries still point to `Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites`.

- [ ] **Step 3: Register Modern UI sheet addresses**

Replace Pixelwood Fantasy Book UI entries with Modern UI sheet entries. Keep the public address constants stable until `StatusHud` is changed; add new sheet constants for the selected Modern UI sheets.

- [ ] **Step 4: Run GREEN**

Run:

```text
Unity MCP tests-run EditMode testClass=UISpriteAddressesTests
```

Expected: PASS.

---

### Task 3: Replace HUD Sprite Selection With Modern UI Sub-Sprites

**Files:**
- Modify: `Assets/Scripts/UI/HUD/StatusHud.cs`
- Modify: `Assets/Tests/PlayMode/InventoryEquipmentUiScenarioTests.cs` or add a focused HUD smoke test if a suitable one already exists.

- [ ] **Step 1: Add a failing UI smoke test**

Assert the HUD preload path can resolve the selected Modern UI sheet and every named sub-sprite used by `StatusHud`.

- [ ] **Step 2: Run RED**

Run the focused PlayMode/EditMode test that covers HUD sprite resolution.

- [ ] **Step 3: Change `StatusHud` to use Modern UI sub-sprites**

Replace book/page/ribbon/button sprite lookups with named Modern UI sub-sprite lookups from the sliced sheets. Keep layout code scoped; do not rewrite inventory or gameplay behavior in this task.

- [ ] **Step 4: Run GREEN and capture screenshot**

Run the focused tests, then capture a Game View screenshot to verify the HUD renders with Modern UI assets.

---

## Self-Review

- Spec coverage: This plan advances the explicit “modernuserinterface-win” and “전체 교체” request by creating the tested slicing/address bridge before runtime replacement. Farm/interiors conversion remains a separate follow-up plan because it touches terrain, props, player/tool sprites, and data wiring.
- Placeholder scan: The first task contains complete test and implementation code. Later tasks intentionally stay as checkpoints because their exact sub-sprite cell choices must be selected after Task 1 slicing exposes the actual sprites in Unity.
- Type consistency: The test uses `ModernUiSliceSetup.Targets`, `SliceTarget`, and `BuildSpriteName`, all defined in Task 1.
