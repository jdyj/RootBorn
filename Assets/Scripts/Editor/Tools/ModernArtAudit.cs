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

                    int width;
                    int height;
                    TryReadPngSize(normalized, out width, out height);
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
                {
                    if (inventory[i].SourcePack == key && inventory[i].Priority == "core") core++;
                }

                sb.AppendLine($"| {key} | {counts[key]} | {core} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Summary By Role");
            sb.AppendLine();
            sb.AppendLine("| Role | PNG count |");
            sb.AppendLine("| --- | ---: |");
            var roles = CountBy(inventory, e => e.Role);
            foreach (var key in SortedKeys(roles))
            {
                sb.AppendLine($"| {key} | {roles[key]} |");
            }

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
            return notes.Count == 0 ? string.Empty : string.Join("; ", notes);
        }

        private static void TryReadPngSize(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                using (var stream = File.OpenRead(assetPath))
                {
                    var header = new byte[24];
                    if (stream.Read(header, 0, header.Length) != header.Length) return;
                    if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47) return;
                    width = ReadBigEndianInt(header, 16);
                    height = ReadBigEndianInt(header, 20);
                }
            }
            catch (IOException)
            {
                width = 0;
                height = 0;
            }
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static Dictionary<string, int> CountBy(IReadOnlyList<AssetEntry> inventory, Func<AssetEntry, string> selector)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < inventory.Count; i++)
            {
                string key = selector(inventory[i]) ?? string.Empty;
                int count;
                counts.TryGetValue(key, out count);
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
            sb.Append('"').Append(key).Append("\": \"").Append(EscapeJson(value ?? string.Empty)).Append('"');
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string EscapeMarkdownCell(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("`", "'");
        }
    }
}
