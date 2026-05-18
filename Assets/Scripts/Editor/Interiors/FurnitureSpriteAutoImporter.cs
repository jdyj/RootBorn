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
    public enum FurnitureSpriteFootprintSource
    {
        FileName,
        PixelSize,
        Fallback
    }

    public readonly struct FurnitureSpriteFootprintResult
    {
        public FurnitureSpriteFootprintResult(Vector2Int footprintSize, FurnitureSpriteFootprintSource source, string message)
        {
            FootprintSize = footprintSize;
            Source = source;
            Message = message ?? string.Empty;
        }

        public Vector2Int FootprintSize { get; }
        public FurnitureSpriteFootprintSource Source { get; }
        public string Message { get; }
    }

    public sealed class FurnitureSpriteImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public FurnitureDefinition FurnitureDefinition { get; set; }
        public string FurnitureAssetPath { get; set; } = string.Empty;
        public string TileAssetPath { get; set; } = string.Empty;
    }

    public static class FurnitureSpriteAutoImporter
    {
        public const int DefaultTilePixelSize = 48;
        public const string DefaultIncomingRoot = "Assets/Incoming/FurnitureSprites";
        public const string DefaultOutputRoot = "Assets/Data/Interiors/Furniture/AutoImported";
        public const string DefaultRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        private static readonly Regex FootprintSuffixRegex = new Regex(@"_(\d+)x(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly string[] DefaultAllowedSurfaceIds = { "house", "town-test" };

        public static FurnitureSpriteFootprintResult ResolveFootprint(string assetPath, int pixelWidth, int pixelHeight, int tilePixelSize = DefaultTilePixelSize)
        {
            if (tilePixelSize <= 0)
            {
                tilePixelSize = DefaultTilePixelSize;
            }

            var fileName = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var match = FootprintSuffixRegex.Match(fileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var width) && int.TryParse(match.Groups[2].Value, out var height) && width > 0 && height > 0)
            {
                return new FurnitureSpriteFootprintResult(new Vector2Int(width, height), FurnitureSpriteFootprintSource.FileName, "Footprint resolved from file name.");
            }

            if (pixelWidth > 0 && pixelHeight > 0)
            {
                var size = new Vector2Int(
                    Mathf.Max(1, Mathf.CeilToInt(pixelWidth / (float)tilePixelSize)),
                    Mathf.Max(1, Mathf.CeilToInt(pixelHeight / (float)tilePixelSize)));
                return new FurnitureSpriteFootprintResult(size, FurnitureSpriteFootprintSource.PixelSize, "Footprint resolved from pixel size using 48px ceiling.");
            }

            return new FurnitureSpriteFootprintResult(Vector2Int.one, FurnitureSpriteFootprintSource.Fallback, "Sprite size is invalid; using 1x1.");
        }

        public static Vector2 ResolveLowerLeftAnchorPivot(int pixelWidth, int pixelHeight, int tilePixelSize = DefaultTilePixelSize)
        {
            if (tilePixelSize <= 0)
            {
                tilePixelSize = DefaultTilePixelSize;
            }

            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                return new Vector2(0.5f, 0.5f);
            }

            var halfTilePixels = tilePixelSize * 0.5f;
            return new Vector2(
                Mathf.Clamp01(halfTilePixels / pixelWidth),
                Mathf.Clamp01(halfTilePixels / pixelHeight));
        }

        public static FurnitureSpriteImportResult ImportSprite(string spriteAssetPath, string outputRoot = DefaultOutputRoot, string registryPath = DefaultRegistryPath)
        {
            var result = new FurnitureSpriteImportResult();
            if (string.IsNullOrWhiteSpace(spriteAssetPath) || !spriteAssetPath.StartsWith("Assets/", StringComparison.Ordinal) || !spriteAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                result.Message = "Furniture sprite must be a PNG under Assets/.";
                return result;
            }

            EnsureSpriteImporter(spriteAssetPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
            if (sprite == null)
            {
                result.Message = "Could not load sprite at " + spriteAssetPath;
                return result;
            }

            var category = ResolveCategory(spriteAssetPath);
            var baseName = StripFootprintSuffix(Path.GetFileNameWithoutExtension(spriteAssetPath));
            var stableId = ToStableId(baseName);
            if (string.IsNullOrEmpty(stableId))
            {
                result.Message = "Could not resolve stable id from " + spriteAssetPath;
                return result;
            }

            var footprint = ResolveFootprint(spriteAssetPath, Mathf.RoundToInt(sprite.rect.width), Mathf.RoundToInt(sprite.rect.height));
            var categoryRoot = CombineAssetPath(outputRoot, category);
            var tileRoot = CombineAssetPath(categoryRoot, "Tiles");
            EnsureAssetFolder(tileRoot);

            var assetName = ToAssetName(baseName);
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (footprint.Source == FurnitureSpriteFootprintSource.Fallback)
            {
                Debug.LogWarning("[FurnitureSpriteAutoImporter] " + footprint.Message + " path=" + spriteAssetPath);
            }

            result.Success = true;
            result.Message = footprint.Message;
            result.FurnitureDefinition = furniture;
            result.FurnitureAssetPath = furniturePath;
            result.TileAssetPath = tilePath;
            return result;
        }

        [MenuItem("Rootborn/Interiors/Import Incoming Furniture Sprites")]
        public static void ImportIncomingFurnitureSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DefaultIncomingRoot });
            int imported = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ImportSprite(path).Success)
                {
                    imported++;
                }
            }

            Debug.Log("[FurnitureSpriteAutoImporter] Imported " + imported + " furniture sprite(s).");
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

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, DefaultTilePixelSize))
            {
                importer.spritePixelsPerUnit = DefaultTilePixelSize;
                changed = true;
            }

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            changed |= ApplyLowerLeftAnchorPivot(importer, width, height);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        public static bool ApplyLowerLeftAnchorPivot(TextureImporter importer, int pixelWidth, int pixelHeight)
        {
            if (importer == null)
            {
                return false;
            }

            var pivot = ResolveLowerLeftAnchorPivot(pixelWidth, pixelHeight);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment == (int)SpriteAlignment.Custom && Vector2.Distance(settings.spritePivot, pivot) <= 0.0001f)
            {
                return false;
            }

            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            return true;
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
            allowed.arraySize = DefaultAllowedSurfaceIds.Length;
            for (int i = 0; i < DefaultAllowedSurfaceIds.Length; i++)
            {
                allowed.GetArrayElementAtIndex(i).stringValue = DefaultAllowedSurfaceIds[i];
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

        private static string ResolveCategory(string spriteAssetPath)
        {
            var normalized = spriteAssetPath.Replace('\\', '/');
            var marker = DefaultIncomingRoot + "/";
            if (normalized.StartsWith(marker, StringComparison.Ordinal) && normalized.Length > marker.Length)
            {
                var rest = normalized.Substring(marker.Length);
                var slash = rest.IndexOf('/');
                if (slash > 0)
                {
                    return SanitizeCategory(rest.Substring(0, slash));
                }
            }

            var parent = Path.GetFileName(Path.GetDirectoryName(normalized));
            return SanitizeCategory(string.IsNullOrWhiteSpace(parent) ? "Furniture" : parent);
        }

        private static string SanitizeCategory(string value)
        {
            var cleaned = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9_ -]", string.Empty).Trim();
            return string.IsNullOrEmpty(cleaned) ? "Furniture" : cleaned;
        }

        private static string StripFootprintSuffix(string fileName)
        {
            return FootprintSuffixRegex.Replace(fileName ?? string.Empty, string.Empty);
        }

        private static string ToStableId(string baseName)
        {
            var normalized = Regex.Replace((baseName ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');
            return normalized;
        }

        private static string ToAssetName(string baseName)
        {
            var normalized = Regex.Replace((baseName ?? string.Empty).Trim(), @"[^A-Za-z0-9]+", "_").Trim('_');
            return string.IsNullOrEmpty(normalized) ? "Furniture" : normalized;
        }

        private static string ToDisplayName(string baseName)
        {
            var words = Regex.Split((baseName ?? string.Empty).Trim(), @"[^A-Za-z0-9]+").Where(word => !string.IsNullOrWhiteSpace(word)).ToArray();
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
            var normalized = folderPath.Replace('\\', '/').TrimEnd('/');
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
    }

    public sealed class FurnitureSpriteAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FurnitureSpriteAutoImporter.DefaultIncomingRoot + "/", StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.spritePixelsPerUnit = FurnitureSpriteAutoImporter.DefaultTilePixelSize;
            textureImporter.GetSourceTextureWidthAndHeight(out var width, out var height);
            FurnitureSpriteAutoImporter.ApplyLowerLeftAnchorPivot(textureImporter, width, height);
        }
    }
}
