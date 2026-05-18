using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.Interiors
{
    public readonly struct ModernInteriorsShadowlessInventoryItem
    {
        public ModernInteriorsShadowlessInventoryItem(string folderName, string sourcePath, int pngCount, string[] sampleFiles)
        {
            FolderName = folderName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            PngCount = pngCount;
            SampleFiles = sampleFiles ?? Array.Empty<string>();
        }

        public string FolderName { get; }
        public string SourcePath { get; }
        public int PngCount { get; }
        public IReadOnlyList<string> SampleFiles { get; }
    }

    public readonly struct ModernInteriorsShadowlessCategory
    {
        public ModernInteriorsShadowlessCategory(string category, bool isHouseFirstCandidate)
        {
            Category = string.IsNullOrWhiteSpace(category) ? "Misc" : category;
            IsHouseFirstCandidate = isHouseFirstCandidate;
        }

        public string Category { get; }
        public bool IsHouseFirstCandidate { get; }
    }

    public readonly struct ModernInteriorsShadowlessCopyPlan
    {
        public ModernInteriorsShadowlessCopyPlan(string sourcePath, string projectAssetPath)
        {
            SourcePath = sourcePath ?? string.Empty;
            ProjectAssetPath = projectAssetPath ?? string.Empty;
        }

        public string SourcePath { get; }
        public string ProjectAssetPath { get; }
    }

    public readonly struct ModernInteriorsShadowlessImportResult
    {
        public ModernInteriorsShadowlessImportResult(int importedCount, int skippedNonHouseFirstCount, int fallbackFootprintCount)
        {
            ImportedCount = importedCount;
            SkippedNonHouseFirstCount = skippedNonHouseFirstCount;
            FallbackFootprintCount = fallbackFootprintCount;
        }

        public int ImportedCount { get; }
        public int SkippedNonHouseFirstCount { get; }
        public int FallbackFootprintCount { get; }
    }

    public static class ModernInteriorsShadowlessFurnitureImporter
    {
        public const string DownloadSourceRoot = "C:/Users/jdyj/Downloads/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48";
        public const string ProjectSourceRoot = "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48";
        public const string OutputRoot = "Assets/Data/Interiors/Furniture/AutoImported/ModernInteriorsShadowless";
        private const string DefaultRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private static readonly string[] AllowedSurfaceIds = { "house", "town-test" };

        public static bool IsAllowedSourcePath(string assetPath)
        {
            var normalized = Normalize(assetPath);
            return normalized.StartsWith(ProjectSourceRoot + "/", StringComparison.Ordinal)
                && normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<ModernInteriorsShadowlessInventoryItem> BuildDownloadInventory()
        {
            if (!Directory.Exists(DownloadSourceRoot))
            {
                return Array.Empty<ModernInteriorsShadowlessInventoryItem>();
            }

            return Directory.GetDirectories(DownloadSourceRoot)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Select(path =>
                {
                    var pngs = Directory.GetFiles(path, "*.png", SearchOption.TopDirectoryOnly)
                        .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
                        .ToArray();
                    return new ModernInteriorsShadowlessInventoryItem(
                        Path.GetFileName(path),
                        Normalize(path),
                        pngs.Length,
                        pngs.Take(5).Select(file => Path.GetFileName(file)).ToArray());
                })
                .ToArray();
        }

        public static ModernInteriorsShadowlessCategory ResolveCategory(string folderName)
        {
            var value = folderName ?? string.Empty;
            if (value.Contains("Living_Room")) return new ModernInteriorsShadowlessCategory("LivingRoom", true);
            if (value.Contains("Bathroom")) return new ModernInteriorsShadowlessCategory("Bathroom", true);
            if (value.Contains("Bedroom")) return new ModernInteriorsShadowlessCategory("Bedroom", true);
            if (value.Contains("Kitchen")) return new ModernInteriorsShadowlessCategory("Kitchen", true);
            if (value.Contains("Japanese")) return new ModernInteriorsShadowlessCategory("Japanese", true);
            if (value.Contains("Condominium")) return new ModernInteriorsShadowlessCategory("Condominium", true);
            if (value.Contains("Museum")) return new ModernInteriorsShadowlessCategory("Museum", false);
            if (value.Contains("Gym")) return new ModernInteriorsShadowlessCategory("Gym", false);
            if (value.Contains("Classroom") || value.Contains("Library")) return new ModernInteriorsShadowlessCategory("Study", false);
            if (value.Contains("Grocery") || value.Contains("Clothing") || value.Contains("Ice_Cream")) return new ModernInteriorsShadowlessCategory("Shop", false);
            return new ModernInteriorsShadowlessCategory("Misc", false);
        }

        public static ModernInteriorsShadowlessCopyPlan BuildCopyPlanForTests(string downloadPngPath)
        {
            var source = Normalize(downloadPngPath);
            var root = Normalize(DownloadSourceRoot);
            if (!source.StartsWith(root + "/", StringComparison.Ordinal))
            {
                return new ModernInteriorsShadowlessCopyPlan(source, string.Empty);
            }

            var relative = source.Substring(root.Length + 1);
            return new ModernInteriorsShadowlessCopyPlan(source, ProjectSourceRoot + "/" + relative);
        }

        [MenuItem("Rootborn/Interiors/Import Modern Interiors Shadowless House Candidates")]
        public static void ImportHouseFirstCandidates()
        {
            var result = ImportHouseFirstCandidatesForTests(ProjectSourceRoot, OutputRoot, DefaultRegistryPath);
            Debug.Log("[ROOTBORN] Modern Interiors shadowless house import complete. Imported=" + result.ImportedCount + ", skipped=" + result.SkippedNonHouseFirstCount + ", fallbackFootprints=" + result.FallbackFootprintCount);
        }

        public static ModernInteriorsShadowlessImportResult ImportHouseFirstCandidatesForTests(string sourceRoot, string outputRoot, string registryPath)
        {
            var normalizedRoot = Normalize(sourceRoot);
            if (string.IsNullOrWhiteSpace(normalizedRoot) || !Directory.Exists(normalizedRoot))
            {
                return new ModernInteriorsShadowlessImportResult(0, 0, 0);
            }

            int imported = 0;
            int skipped = 0;
            int fallback = 0;
            var folders = Directory.GetDirectories(normalizedRoot).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal).ToArray();
            for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
            {
                var folder = folders[folderIndex];
                var category = ResolveCategory(Path.GetFileName(folder));
                var pngs = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal).ToArray();
                if (!category.IsHouseFirstCandidate)
                {
                    skipped += pngs.Length;
                    continue;
                }

                for (int i = 0; i < pngs.Length; i++)
                {
                    if (ImportOne(Normalize(pngs[i]), outputRoot, registryPath, category.Category, out var usedFallback))
                    {
                        imported++;
                        if (usedFallback)
                        {
                            fallback++;
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new ModernInteriorsShadowlessImportResult(imported, skipped, fallback);
        }

        private static bool ImportOne(string spriteAssetPath, string outputRoot, string registryPath, string category, out bool usedFallback)
        {
            usedFallback = false;
            if (string.IsNullOrWhiteSpace(spriteAssetPath) || !spriteAssetPath.StartsWith("Assets/", StringComparison.Ordinal) || !spriteAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureSpriteImporter(spriteAssetPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
            if (sprite == null)
            {
                return false;
            }

            var baseName = Path.GetFileNameWithoutExtension(spriteAssetPath) ?? string.Empty;
            var assetName = ToAssetName(baseName);
            var stableId = "modern.interiors.shadowless." + ToStableId(category) + "." + ToStableId(baseName);
            var footprint = FurnitureSpriteAutoImporter.ResolveFootprint(spriteAssetPath, Mathf.RoundToInt(sprite.rect.width), Mathf.RoundToInt(sprite.rect.height));
            usedFallback = footprint.Source == FurnitureSpriteFootprintSource.Fallback;

            var categoryRoot = CombineAssetPath(outputRoot, category);
            var tileRoot = CombineAssetPath(categoryRoot, "Tiles");
            EnsureAssetFolder(tileRoot);

            var tilePath = CombineAssetPath(tileRoot, assetName + "_Tile.asset");
            var furniturePath = CombineAssetPath(categoryRoot, "Furniture_" + assetName + ".asset");

            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.sprite = sprite;
            tile.name = assetName + "_Tile";
            EditorUtility.SetDirty(tile);

            var furniture = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(furniturePath);
            if (furniture == null)
            {
                furniture = ScriptableObject.CreateInstance<FurnitureDefinition>();
                AssetDatabase.CreateAsset(furniture, furniturePath);
            }

            ConfigureFurniture(furniture, stableId, ToDisplayName(baseName), category, tile, footprint.FootprintSize);
            EditorUtility.SetDirty(furniture);
            RegisterFurniture(registryPath, furniture);
            return true;
        }

        private static void EnsureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, FurnitureSpriteAutoImporter.DefaultTilePixelSize))
            {
                importer.spritePixelsPerUnit = FurnitureSpriteAutoImporter.DefaultTilePixelSize;
                changed = true;
            }

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            changed |= FurnitureSpriteAutoImporter.ApplyLowerLeftAnchorPivot(importer, width, height);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureFurniture(FurnitureDefinition furniture, string stableId, string displayName, string category, Tile tile, Vector2Int footprintSize)
        {
            var serialized = new SerializedObject(furniture);
            serialized.FindProperty("_stableId").stringValue = stableId;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_category").stringValue = category;
            serialized.FindProperty("_blocksMovement").boolValue = true;
            serialized.FindProperty("_unlockToken").stringValue = string.Empty;

            var allowed = serialized.FindProperty("_allowedSurfaceIds");
            allowed.arraySize = AllowedSurfaceIds.Length;
            for (int i = 0; i < AllowedSurfaceIds.Length; i++)
            {
                allowed.GetArrayElementAtIndex(i).stringValue = AllowedSurfaceIds[i];
            }

            var tileParts = serialized.FindProperty("_tileParts");
            tileParts.arraySize = 1;
            var part = tileParts.GetArrayElementAtIndex(0);
            part.FindPropertyRelative("_localCell").vector2IntValue = Vector2Int.zero;
            part.FindPropertyRelative("_tile").objectReferenceValue = tile;

            var footprintCells = serialized.FindProperty("_footprintCells");
            var cells = BuildFootprintCells(footprintSize).ToArray();
            footprintCells.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                footprintCells.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<Vector2Int> BuildFootprintCells(Vector2Int footprintSize)
        {
            var width = Mathf.Max(1, footprintSize.x);
            var height = Mathf.Max(1, footprintSize.y);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private static void RegisterFurniture(string registryPath, FurnitureDefinition furniture)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(registryPath);
            if (registry == null || furniture == null)
            {
                return;
            }

            var serialized = new SerializedObject(registry);
            var definitions = serialized.FindProperty("_furnitureDefinitions");
            for (int i = 0; i < definitions.arraySize; i++)
            {
                if (definitions.GetArrayElementAtIndex(i).objectReferenceValue == furniture)
                {
                    return;
                }
            }

            definitions.InsertArrayElementAtIndex(definitions.arraySize);
            definitions.GetArrayElementAtIndex(definitions.arraySize - 1).objectReferenceValue = furniture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static string ToAssetName(string baseName)
        {
            var normalized = Regex.Replace((baseName ?? string.Empty).Trim(), @"[^A-Za-z0-9]+", "_").Trim('_');
            return string.IsNullOrEmpty(normalized) ? "Furniture" : normalized;
        }

        private static string ToStableId(string value)
        {
            return Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');
        }

        private static string ToDisplayName(string baseName)
        {
            var words = Regex.Split((baseName ?? string.Empty).Trim(), @"[^A-Za-z0-9]+")
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .ToArray();
            if (words.Length == 0)
            {
                return "Furniture";
            }

            for (int i = 0; i < words.Length; i++)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }

            return string.Join(" ", words);
        }

        private static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\')).Replace('\\', '/');
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var normalized = Normalize(folderPath).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("Asset folder must be under Assets/: " + folderPath, nameof(folderPath));
            }

            var current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }
    }
}
