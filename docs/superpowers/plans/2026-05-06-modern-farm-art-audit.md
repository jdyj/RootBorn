# Modern Farm Art Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 1 audit tooling for the LimeZu Modern art conversion: classify modern asset PNGs, report Pixelwood references, and generate reviewable `docs/art` inventory files.

**Architecture:** Add one Editor-only audit tool under `Rootborn.Editor.Tools` and one EditMode test fixture under `Rootborn.Tests.EditMode`. The tool does not modify Unity importers, scenes, prefabs, or ScriptableObjects; it only reads files and writes markdown/json reports. Later conversion phases will consume these reports.

**Tech Stack:** Unity 6000.3, C# Editor tooling, NUnit EditMode tests, `UnityEditor.AssetDatabase`, `System.IO`, `System.Text.Json` is not used because Unity profiles can vary; JSON is written with a small string escaper.

---

## Scope Boundary

This plan implements only Phase 1 from the design spec:

- modern asset inventory markdown/json
- Pixelwood reference report markdown
- classification rules for source pack, role, grid candidate, slice mode, and priority
- tests proving the reports include the core LimeZu packs and current Pixelwood references

This plan does not slice sprites, change TextureImporter settings, modify Addressables, change `UISpriteAddresses`, edit scenes, edit prefabs, or rewire ScriptableObjects.

## File Structure

- Create `Assets/Scripts/Editor/Tools/ModernArtAudit.cs`
  - Responsibility: scan local asset packs, classify PNGs, find Pixelwood references, and write reports.
  - Must be created with MCP `script-update-or-create`, not direct shell writes.

- Create `Assets/Tests/EditMode/ModernArtAuditTests.cs`
  - Responsibility: TDD coverage for pack discovery, classification, report generation, and Pixelwood reference detection.
  - Must be created with MCP `script-update-or-create`, not direct shell writes.

- Create by tool execution:
  - `docs/art/modern-asset-inventory.md`
  - `docs/art/modern-asset-inventory.json`
  - `docs/art/pixelwood-reference-report.md`

## Task 1: Modern Asset Inventory Model And Classification

**Files:**
- Create: `Assets/Tests/EditMode/ModernArtAuditTests.cs`
- Create: `Assets/Scripts/Editor/Tools/ModernArtAudit.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/ModernArtAuditTests.cs` using MCP `script-update-or-create` with this content:

```csharp
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernArtAuditTests
    {
        [Test]
        public void BuildInventory_IncludesCoreModernPacks()
        {
            var inventory = ModernArtAudit.BuildInventory();

            Assert.Greater(inventory.Length, 0);
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern Interiors" && x.Path.StartsWith("Assets/moderninteriors-win/")));
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern Farm" && x.Path.StartsWith("Assets/Modern_Farm_v1.2/")));
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern User Interface" && x.Path.StartsWith("Assets/modernuserinterface-win/")));
        }

        [Test]
        public void BuildInventory_ClassifiesKnownUiAndFarmPaths()
        {
            var inventory = ModernArtAudit.BuildInventory();

            var uiSheet = inventory.FirstOrDefault(x => x.Path == "Assets/modernuserinterface-win/16x16/UI_16x16.png");
            Assert.IsNotNull(uiSheet);
            Assert.AreEqual("ui", uiSheet.Role);
            Assert.AreEqual("16x16", uiSheet.GridCandidate);
            Assert.AreEqual("grid", uiSheet.SliceMode);
            Assert.AreEqual("core", uiSheet.Priority);

            var farmIcon = inventory.FirstOrDefault(x => x.Path.Contains("/Icons/") && x.SourcePack == "Modern Farm");
            Assert.IsNotNull(farmIcon);
            Assert.AreEqual("icon", farmIcon.Role);
            Assert.AreEqual("core", farmIcon.Priority);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
# In Unity Test Runner MCP:
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected: compile failure because `Rootborn.Editor.Tools.ModernArtAudit` does not exist.

- [ ] **Step 3: Implement minimal inventory scanner**

Create `Assets/Scripts/Editor/Tools/ModernArtAudit.cs` using MCP `script-update-or-create` with this content:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernArtAudit
    {
        public const string InventoryMarkdownPath = "docs/art/modern-asset-inventory.md";
        public const string InventoryJsonPath = "docs/art/modern-asset-inventory.json";
        public const string PixelwoodReferenceReportPath = "docs/art/pixelwood-reference-report.md";

        private static readonly PackRoot[] PackRoots =
        {
            new PackRoot("Modern Interiors", "Assets/moderninteriors-win"),
            new PackRoot("Modern Farm", "Assets/Modern_Farm_v1.2"),
            new PackRoot("Modern User Interface", "Assets/modernuserinterface-win"),
            new PackRoot("Modern Office Revamped", "Assets/Modern_Office_Revamped_v1.2"),
            new PackRoot("Modern Interiors RPG Maker Version", "Assets/Modern_Interiors_RPG_Maker_Version"),
        };

        private static readonly string[] PixelwoodScanRoots =
        {
            "Assets/Scripts",
            "Assets/Data",
            "Assets/Scenes",
            "Assets/Prefabs",
            "Assets/AddressableAssetsData",
        };

        public readonly struct PackRoot
        {
            public PackRoot(string sourcePack, string rootPath)
            {
                SourcePack = sourcePack;
                RootPath = NormalizePath(rootPath);
            }

            public string SourcePack { get; }
            public string RootPath { get; }
        }

        public sealed class AssetEntry
        {
            public string Path;
            public string SourcePack;
            public string TopFolder;
            public int Width;
            public int Height;
            public string GridCandidate;
            public string Role;
            public string SliceMode;
            public string Priority;
            public string Notes;
        }

        public sealed class PixelwoodReference
        {
            public string Path;
            public int Line;
            public string Text;
        }

        [MenuItem("Rootborn/Modern Art/Generate Audit Reports")]
        public static void GenerateReportsMenu()
        {
            GenerateReports();
        }

        public static AssetEntry[] BuildInventory()
        {
            var result = new List<AssetEntry>(8192);
            foreach (var pack in PackRoots)
            {
                if (!Directory.Exists(pack.RootPath)) continue;
                foreach (string file in Directory.GetFiles(pack.RootPath, "*.*", SearchOption.AllDirectories))
                {
                    string normalized = NormalizePath(file);
                    if (!IsPng(normalized)) continue;

                    int width = 0;
                    int height = 0;
                    TryReadTextureSize(normalized, out width, out height);
                    result.Add(CreateEntry(pack, normalized, width, height));
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return result.ToArray();
        }

        public static PixelwoodReference[] FindPixelwoodReferences()
        {
            var result = new List<PixelwoodReference>(256);
            foreach (string root in PixelwoodScanRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    string normalized = NormalizePath(file);
                    if (!IsTextLike(normalized)) continue;
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(normalized);
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.IndexOf("Pixelwood", StringComparison.OrdinalIgnoreCase) < 0 &&
                            line.IndexOf("Fantasy Book UI", StringComparison.OrdinalIgnoreCase) < 0 &&
                            line.IndexOf("Wood UI", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        result.Add(new PixelwoodReference
                        {
                            Path = normalized,
                            Line = i + 1,
                            Text = line.Trim(),
                        });
                    }
                }
            }

            result.Sort((a, b) =>
            {
                int path = string.CompareOrdinal(a.Path, b.Path);
                return path != 0 ? path : a.Line.CompareTo(b.Line);
            });
            return result.ToArray();
        }

        public static void GenerateReports()
        {
            EnsureDirectory("docs/art");
            var inventory = BuildInventory();
            var references = FindPixelwoodReferences();
            File.WriteAllText(InventoryMarkdownPath, BuildInventoryMarkdown(inventory), Encoding.UTF8);
            File.WriteAllText(InventoryJsonPath, BuildInventoryJson(inventory), Encoding.UTF8);
            File.WriteAllText(PixelwoodReferenceReportPath, BuildPixelwoodReferenceMarkdown(references), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/ModernArtAudit] Wrote {InventoryMarkdownPath}, {InventoryJsonPath}, {PixelwoodReferenceReportPath}");
        }

        public static string BuildInventoryMarkdown(IReadOnlyList<AssetEntry> inventory)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine("# Modern Asset Inventory");
            sb.AppendLine();
            sb.AppendLine("Generated by `Rootborn/Modern Art/Generate Audit Reports`.");
            sb.AppendLine();
            sb.AppendLine("## Summary By Source Pack");
            sb.AppendLine();
            sb.AppendLine("| Source pack | PNG count | Core count |");
            sb.AppendLine("| --- | ---: | ---: |");

            var counts = CountBy(inventory, e => e.SourcePack);
            foreach (var key in SortedKeys(counts))
            {
                int core = 0;
                for (int i = 0; i < inventory.Count; i++)
                    if (inventory[i].SourcePack == key && inventory[i].Priority == "core")
                        core++;
                sb.AppendLine($"| {key} | {counts[key]} | {core} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Summary By Role");
            sb.AppendLine();
            sb.AppendLine("| Role | PNG count |");
            sb.AppendLine("| --- | ---: |");
            var roles = CountBy(inventory, e => e.Role);
            foreach (var key in SortedKeys(roles))
                sb.AppendLine($"| {key} | {roles[key]} |");

            sb.AppendLine();
            sb.AppendLine("## Core Candidates");
            sb.AppendLine();
            sb.AppendLine("| Source pack | Role | Grid | Slice | Path | Notes |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            for (int i = 0; i < inventory.Count; i++)
            {
                var e = inventory[i];
                if (e.Priority != "core") continue;
                sb.AppendLine($"| {e.SourcePack} | {e.Role} | {e.GridCandidate} | {e.SliceMode} | `{e.Path}` | {e.Notes} |");
            }

            return sb.ToString();
        }

        public static string BuildInventoryJson(IReadOnlyList<AssetEntry> inventory)
        {
            var sb = new StringBuilder(1024 * 1024);
            sb.AppendLine("[");
            for (int i = 0; i < inventory.Count; i++)
            {
                var e = inventory[i];
                sb.Append("  {");
                AppendJsonField(sb, "path", e.Path); sb.Append(", ");
                AppendJsonField(sb, "sourcePack", e.SourcePack); sb.Append(", ");
                AppendJsonField(sb, "topFolder", e.TopFolder); sb.Append(", ");
                sb.Append("\"width\": ").Append(e.Width).Append(", ");
                sb.Append("\"height\": ").Append(e.Height).Append(", ");
                AppendJsonField(sb, "gridCandidate", e.GridCandidate); sb.Append(", ");
                AppendJsonField(sb, "role", e.Role); sb.Append(", ");
                AppendJsonField(sb, "sliceMode", e.SliceMode); sb.Append(", ");
                AppendJsonField(sb, "priority", e.Priority); sb.Append(", ");
                AppendJsonField(sb, "notes", e.Notes);
                sb.Append(i == inventory.Count - 1 ? "}" : "},");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        public static string BuildPixelwoodReferenceMarkdown(IReadOnlyList<PixelwoodReference> references)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine("# Pixelwood Reference Report");
            sb.AppendLine();
            sb.AppendLine("Generated by `Rootborn/Modern Art/Generate Audit Reports`.");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Text |");
            sb.AppendLine("| --- | ---: | --- |");
            for (int i = 0; i < references.Count; i++)
            {
                var r = references[i];
                sb.AppendLine($"| `{r.Path}` | {r.Line} | `{EscapeMarkdownCell(r.Text)}` |");
            }
            return sb.ToString();
        }

        private static AssetEntry CreateEntry(PackRoot pack, string path, int width, int height)
        {
            string lower = path.ToLowerInvariant();
            string role = ClassifyRole(pack.SourcePack, lower);
            string grid = ClassifyGrid(lower, width, height);
            string slice = ClassifySliceMode(role, grid, lower);
            string priority = ClassifyPriority(pack.SourcePack, role, lower);
            return new AssetEntry
            {
                Path = path,
                SourcePack = pack.SourcePack,
                TopFolder = GetTopFolder(pack.RootPath, path),
                Width = width,
                Height = height,
                GridCandidate = grid,
                Role = role,
                SliceMode = slice,
                Priority = priority,
                Notes = BuildNotes(width, height, lower),
            };
        }

        private static string ClassifyRole(string sourcePack, string lowerPath)
        {
            if (lowerPath.Contains("/palette")) return "palette";
            if (lowerPath.Contains("preview") || lowerPath.Contains("guide")) return "preview";
            if (lowerPath.Contains("/icons/") || lowerPath.Contains("icon")) return "icon";
            if (sourcePack == "Modern User Interface" || lowerPath.Contains("user_interface") || lowerPath.Contains("/ui_")) return "ui";
            if (lowerPath.Contains("portrait")) return "portrait";
            if (lowerPath.Contains("character") || lowerPath.Contains("farmer") || lowerPath.Contains("generator_pieces")) return "character";
            if (lowerPath.Contains("animal") || lowerPath.Contains("cow") || lowerPath.Contains("chicken") || lowerPath.Contains("sheep")) return "animal";
            if (lowerPath.Contains("crop") || lowerPath.Contains("seed")) return "crop";
            if (lowerPath.Contains("animated")) return "animatedObject";
            if (lowerPath.Contains("tile") || lowerPath.Contains("floor") || lowerPath.Contains("wall") || lowerPath.Contains("room_builder")) return "tile";
            return "prop";
        }

        private static string ClassifyGrid(string lowerPath, int width, int height)
        {
            if (lowerPath.Contains("48x48")) return "48x48";
            if (lowerPath.Contains("32x32")) return "32x32";
            if (lowerPath.Contains("16x16")) return "16x16";
            if (width > 0 && height > 0)
            {
                if (width % 48 == 0 && height % 48 == 0) return "48x48";
                if (width % 32 == 0 && height % 32 == 0) return "32x32";
                if (width % 16 == 0 && height % 16 == 0) return "16x16";
            }
            return "single";
        }

        private static string ClassifySliceMode(string role, string grid, string lowerPath)
        {
            if (role == "preview" || role == "palette") return "ignore";
            if (role == "ui" && (lowerPath.Contains("frame") || lowerPath.Contains("button") || lowerPath.Contains("panel"))) return "nineSlice";
            if (grid == "16x16" || grid == "32x32" || grid == "48x48") return "grid";
            return "single";
        }

        private static string ClassifyPriority(string sourcePack, string role, string lowerPath)
        {
            if (role == "preview" || role == "palette") return "ignore";
            if (lowerPath.Contains("/rpg_maker") || lowerPath.Contains("/old/")) return "optional";
            if (sourcePack == "Modern Farm" && (role == "crop" || role == "tile" || role == "icon" || role == "animal" || role == "prop" || role == "character")) return "core";
            if (sourcePack == "Modern User Interface" && (role == "ui" || role == "icon" || role == "portrait")) return "core";
            if (sourcePack == "Modern Interiors" && (role == "character" || role == "animatedObject" || role == "prop" || role == "tile")) return "support";
            return "support";
        }

        private static string BuildNotes(int width, int height, string lowerPath)
        {
            var notes = new List<string>();
            if (width > 0 && height > 0) notes.Add(width + "x" + height);
            if (lowerPath.Contains("rpg_maker")) notes.Add("RPG Maker compatibility candidate");
            if (lowerPath.Contains("old/")) notes.Add("old asset candidate");
            if (notes.Count == 0) return "";
            return string.Join("; ", notes);
        }

        private static void TryReadTextureSize(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) return;
            width = texture.width;
            height = texture.height;
        }

        private static Dictionary<string, int> CountBy(IReadOnlyList<AssetEntry> inventory, Func<AssetEntry, string> selector)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < inventory.Count; i++)
            {
                string key = selector(inventory[i]) ?? "";
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            return counts;
        }

        private static List<string> SortedKeys(Dictionary<string, int> dictionary)
        {
            var keys = new List<string>(dictionary.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static string GetTopFolder(string root, string path)
        {
            string relative = path.StartsWith(root + "/", StringComparison.Ordinal) ? path.Substring(root.Length + 1) : path;
            int slash = relative.IndexOf('/');
            return slash < 0 ? relative : relative.Substring(0, slash);
        }

        private static bool IsPng(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextLike(string path)
        {
            return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void AppendJsonField(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\": \"").Append(EscapeJson(value ?? "")).Append('"');
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string EscapeMarkdownCell(string value)
        {
            return (value ?? "").Replace("|", "\\|").Replace("`", "'");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
# In Unity Test Runner MCP:
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected: both tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Tests/EditMode/ModernArtAuditTests.cs Assets/Tests/EditMode/ModernArtAuditTests.cs.meta Assets/Scripts/Editor/Tools/ModernArtAudit.cs Assets/Scripts/Editor/Tools/ModernArtAudit.cs.meta
git commit -m "[TOOL][TEST] 모던 아트 인벤토리 분류 도구 추가" -m "LimeZu Modern 계열 에셋을 읽어 source pack, role, grid, slice mode, priority로 분류하는 Editor 전용 감사 도구와 EditMode 테스트를 추가한다."
```

## Task 2: Pixelwood Reference Report

**Files:**
- Modify: `Assets/Tests/EditMode/ModernArtAuditTests.cs`
- Modify: `Assets/Scripts/Editor/Tools/ModernArtAudit.cs`

- [ ] **Step 1: Add failing tests for Pixelwood reference detection**

Append these tests inside `ModernArtAuditTests` before the class closing brace:

```csharp
        [Test]
        public void FindPixelwoodReferences_FindsCurrentCodeReferences()
        {
            var references = ModernArtAudit.FindPixelwoodReferences();

            Assert.IsTrue(references.Any(x => x.Path == "Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs"));
            Assert.IsTrue(references.Any(x => x.Path == "Assets/Scripts/Editor/Tools/AddressablesSetup.cs"));
            Assert.IsTrue(references.Any(x => x.Text.Contains("Pixelwood")));
        }

        [Test]
        public void PixelwoodReferenceMarkdown_ContainsFileAndLine()
        {
            var references = ModernArtAudit.FindPixelwoodReferences();
            string markdown = ModernArtAudit.BuildPixelwoodReferenceMarkdown(references);

            Assert.IsTrue(markdown.Contains("# Pixelwood Reference Report"));
            Assert.IsTrue(markdown.Contains("Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs"));
            Assert.IsTrue(markdown.Contains("| Line |"));
        }
```

- [ ] **Step 2: Run tests to verify they fail if Task 1 implementation lacks reference support**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected if Task 1 implementation was copied exactly: these tests pass because reference support was included with the minimal implementation. If they pass immediately, document that Task 1 already supplied the behavior and continue. If a worker split Task 1 smaller and omitted `FindPixelwoodReferences`, expected failure is a missing method compile error.

- [ ] **Step 3: Implement reference support if missing**

If the tests fail due to missing reference support, add the `PixelwoodReference` class, `FindPixelwoodReferences`, `BuildPixelwoodReferenceMarkdown`, `PixelwoodScanRoots`, and `IsTextLike` methods from Task 1's implementation block to `ModernArtAudit.cs` using MCP `script-update-or-create`.

- [ ] **Step 4: Run tests to verify they pass**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected: all `ModernArtAuditTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Tests/EditMode/ModernArtAuditTests.cs Assets/Scripts/Editor/Tools/ModernArtAudit.cs
git commit -m "[TOOL][TEST] Pixelwood 참조 리포트 검증 추가" -m "모던 전환 전 legacy Pixelwood 참조를 파일과 라인 단위로 수집하고 마크다운 리포트로 출력하는 테스트를 추가한다."
```

## Task 3: Generate `docs/art` Reports

**Files:**
- Modify: `Assets/Tests/EditMode/ModernArtAuditTests.cs`
- Generated: `docs/art/modern-asset-inventory.md`
- Generated: `docs/art/modern-asset-inventory.json`
- Generated: `docs/art/pixelwood-reference-report.md`

- [ ] **Step 1: Add failing tests for report file generation**

Append this test inside `ModernArtAuditTests` before the class closing brace:

```csharp
        [Test]
        public void GenerateReports_WritesDocsArtFiles()
        {
            ModernArtAudit.GenerateReports();

            Assert.IsTrue(System.IO.File.Exists(ModernArtAudit.InventoryMarkdownPath));
            Assert.IsTrue(System.IO.File.Exists(ModernArtAudit.InventoryJsonPath));
            Assert.IsTrue(System.IO.File.Exists(ModernArtAudit.PixelwoodReferenceReportPath));

            string markdown = System.IO.File.ReadAllText(ModernArtAudit.InventoryMarkdownPath);
            Assert.IsTrue(markdown.Contains("# Modern Asset Inventory"));
            Assert.IsTrue(markdown.Contains("Modern Farm"));
            Assert.IsTrue(markdown.Contains("Modern User Interface"));

            string json = System.IO.File.ReadAllText(ModernArtAudit.InventoryJsonPath);
            Assert.IsTrue(json.StartsWith("["));
            Assert.IsTrue(json.Contains("\"sourcePack\": \"Modern Farm\""));

            string refs = System.IO.File.ReadAllText(ModernArtAudit.PixelwoodReferenceReportPath);
            Assert.IsTrue(refs.Contains("# Pixelwood Reference Report"));
            Assert.IsTrue(refs.Contains("Pixelwood"));
        }
```

- [ ] **Step 2: Run tests to verify generation behavior**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected if Task 1 implementation was copied exactly: all tests pass and the three docs files are created. If `GenerateReports` is missing, expected failure is a missing method compile error.

- [ ] **Step 3: Implement report generation if missing**

If missing, add `GenerateReports`, `BuildInventoryMarkdown`, `BuildInventoryJson`, `EnsureDirectory`, `AppendJsonField`, `EscapeJson`, and `EscapeMarkdownCell` from Task 1's implementation block to `ModernArtAudit.cs` using MCP `script-update-or-create`.

- [ ] **Step 4: Run targeted tests**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected: all `ModernArtAuditTests` pass.

- [ ] **Step 5: Inspect generated report sizes**

Run:

```powershell
Get-Item docs/art/modern-asset-inventory.md, docs/art/modern-asset-inventory.json, docs/art/pixelwood-reference-report.md | Select-Object Name,Length
```

Expected: all three files exist and have non-zero lengths. The JSON file will be large because the project contains tens of thousands of PNG files.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Tests/EditMode/ModernArtAuditTests.cs Assets/Scripts/Editor/Tools/ModernArtAudit.cs docs/art/modern-asset-inventory.md docs/art/modern-asset-inventory.json docs/art/pixelwood-reference-report.md
git commit -m "[TOOL][DOCS][TEST] 모던 아트 감사 리포트 생성" -m "Modern Farm 전환 Phase 1 산출물로 에셋 인벤토리와 Pixelwood 참조 리포트를 생성하고 검증한다."
```

## Task 4: Phase 1 Verification

**Files:**
- Read: `docs/superpowers/specs/2026-05-06-modern-farm-art-conversion-design.md`
- Read: `docs/art/modern-asset-inventory.md`
- Read: `docs/art/pixelwood-reference-report.md`

- [ ] **Step 1: Run the focused EditMode tests**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.ModernArtAuditTests includeMessages=true
```

Expected: all `ModernArtAuditTests` pass.

- [ ] **Step 2: Run static gate tests because new Editor code scans paths and emits reports**

Run:

```powershell
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests includeMessages=true
```

Expected: static runtime usage gate remains unchanged. Editor-only `ModernArtAudit.cs` is outside runtime scan roots and should not add runtime violations.

- [ ] **Step 3: Verify no unintended staged files are present**

Run:

```powershell
git status --short
```

Expected: only known pre-existing dirty files plus Phase 1 files if they have not yet been committed. Do not stage unrelated dirty files.

- [ ] **Step 4: Final Phase 1 commit if any verification-only changes remain**

If `docs/art` files changed from the verification run, commit only those files:

```powershell
git add docs/art/modern-asset-inventory.md docs/art/modern-asset-inventory.json docs/art/pixelwood-reference-report.md
git commit -m "[DOCS] 모던 아트 감사 리포트 최신화" -m "검증 실행으로 갱신된 Phase 1 에셋 인벤토리와 Pixelwood 참조 리포트를 반영한다."
```

If there are no Phase 1 changes left, do not create an empty commit.

## Self-Review Notes

- Spec coverage: this plan covers Phase 1 audit and catalog reports only. UI conversion, icon rewiring, farm visuals, character animation, and Pixelwood removal remain separate future plans.
- TDD order: each production Editor behavior has a test step before implementation.
- AGENTS compliance: all new C# files must be created with `script-update-or-create`; this plan does not instruct shell writes to `Assets/**/*.cs`.
- Data-driven rule: this phase only classifies art files and does not add entity-specific runtime branches.
