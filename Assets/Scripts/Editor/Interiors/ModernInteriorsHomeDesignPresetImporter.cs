using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.Interiors
{
    public readonly struct HomeDesignRoomPresetImportResult
    {
        public HomeDesignRoomPresetImportResult(int importedCount, int exactGridCount, int deferredNonExactGridCount)
        {
            ImportedCount = importedCount;
            ExactGridCount = exactGridCount;
            DeferredNonExactGridCount = deferredNonExactGridCount;
        }

        public int ImportedCount { get; }
        public int ExactGridCount { get; }
        public int DeferredNonExactGridCount { get; }
    }

    public static class ModernInteriorsHomeDesignPresetImporter
    {
        public const string DownloadSourceRoot = "C:/Users/jdyj/Downloads/moderninteriors-win/6_Home_Designs";
        public const string ProjectSourceRoot = "Assets/moderninteriors-win/6_Home_Designs";
        public const string OutputRoot = "Assets/Data/Interiors/RoomPresets/AutoImported/ModernInteriorsHomeDesigns";
        public const int CellPixelSize = 48;

        private static readonly string[] CondominiumLayer1Files =
        {
            "Condominium_Designs/48x48/Condominium_Design_2_layer_1_48x48.png",
            "Condominium_Designs/48x48/Condominium_Design_layer_1_48x48.png",
        };

        [MenuItem("Rootborn/Interiors/Import Modern Interiors Home Design Presets")]
        public static void ImportCondominiumSamples()
        {
            var result = ImportCondominiumSamplesForTests(DownloadSourceRoot, ProjectSourceRoot, OutputRoot);
            AssetDatabase.Refresh();
            Debug.Log("[ROOTBORN] Modern Interiors Home Design preset import complete. Imported=" + result.ImportedCount + ", exactGrid=" + result.ExactGridCount + ", deferred=" + result.DeferredNonExactGridCount);
        }

        public static HomeDesignRoomPresetImportResult ImportCondominiumSamplesForTests(string downloadRoot, string projectRoot, string outputRoot)
        {
            int imported = 0;
            int exact = 0;
            int deferred = 0;

            for (int i = 0; i < CondominiumLayer1Files.Length; i++)
            {
                var relativePath = CondominiumLayer1Files[i];
                var sourcePath = Normalize(Path.Combine(downloadRoot, relativePath));
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var analysis = HomeDesignLayer1GridAnalyzer.Analyze(sourcePath, CellPixelSize);
                if (!analysis.IsExactGrid)
                {
                    deferred++;
                    continue;
                }

                exact++;
                var projectAssetPath = NormalizeAssetPath(projectRoot + "/" + relativePath);
                CopyToAssetPath(sourcePath, projectAssetPath);
                if (ImportOne(projectAssetPath, outputRoot, analysis))
                {
                    imported++;
                }
            }

            AssetDatabase.SaveAssets();
            return new HomeDesignRoomPresetImportResult(imported, exact, deferred);
        }

        private static bool ImportOne(string layer1AssetPath, string outputRoot, HomeDesignLayer1GridAnalysis analysis)
        {
            ConfigureLayerTexture(layer1AssetPath, analysis.CellSize);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(layer1AssetPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0)
            {
                return false;
            }

            var designName = ResolveDesignName(layer1AssetPath);
            var designRoot = CombineAssetPath(outputRoot, designName);
            var tileRoot = CombineAssetPath(designRoot, "Tiles");
            EnsureAssetFolder(tileRoot);

            var cells = new List<InteriorRoomPresetTileCell>(sprites.Length);
            for (int row = 0; row < analysis.CellSize.y; row++)
            {
                for (int column = 0; column < analysis.CellSize.x; column++)
                {
                    var spriteName = BuildSpriteName(designName, row, column);
                    var sprite = sprites.FirstOrDefault(item => item.name == spriteName);
                    if (sprite == null)
                    {
                        return false;
                    }

                    var tilePath = CombineAssetPath(tileRoot, spriteName + "_Tile.asset");
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                    if (tile == null)
                    {
                        tile = ScriptableObject.CreateInstance<Tile>();
                        AssetDatabase.CreateAsset(tile, tilePath);
                    }

                    tile.name = spriteName + "_Tile";
                    tile.sprite = sprite;
                    EditorUtility.SetDirty(tile);
                    cells.Add(new InteriorRoomPresetTileCell(new Vector2Int(column, analysis.CellSize.y - 1 - row), tile));
                }
            }

            var presetPath = CombineAssetPath(designRoot, "InteriorRoomPreset_" + designName + ".asset");
            var preset = AssetDatabase.LoadAssetAtPath<InteriorRoomPresetDefinition>(presetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<InteriorRoomPresetDefinition>();
                AssetDatabase.CreateAsset(preset, presetPath);
            }

            string stableId = ToStableId(designName);
            string displayName = ToDisplayName(designName);
            preset.Configure(stableId, displayName, analysis.CellSize, cells);
            var serializedPreset = new SerializedObject(preset);
            serializedPreset.FindProperty("_stableId").stringValue = stableId;
            serializedPreset.FindProperty("_displayName").stringValue = displayName;
            serializedPreset.FindProperty("_size").vector2IntValue = analysis.CellSize;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
            return true;
        }

        private static void ConfigureLayerTexture(string assetPath, Vector2Int cellSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Home design layer_1 asset is not a texture: " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = CellPixelSize;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var metas = new List<SpriteMetaData>(cellSize.x * cellSize.y);
            var designName = ResolveDesignName(assetPath);
            for (int row = 0; row < cellSize.y; row++)
            {
                for (int column = 0; column < cellSize.x; column++)
                {
                    int x = column * CellPixelSize;
                    int yFromBottom = (cellSize.y - 1 - row) * CellPixelSize;
                    metas.Add(new SpriteMetaData
                    {
                        name = BuildSpriteName(designName, row, column),
                        rect = new Rect(x, yFromBottom, CellPixelSize, CellPixelSize),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        border = Vector4.zero,
                    });
                }
            }

#pragma warning disable CS0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void CopyToAssetPath(string sourcePath, string assetPath)
        {
            EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty);
            var fullTarget = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullTarget) ?? string.Empty);
            File.Copy(sourcePath, fullTarget, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ResolveDesignName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            name = name.Replace("_layer_1_48x48", string.Empty, StringComparison.OrdinalIgnoreCase);
            name = name.Replace("_Layer_1_48x48", string.Empty, StringComparison.OrdinalIgnoreCase);
            name = name.Replace("_layer_1", string.Empty, StringComparison.OrdinalIgnoreCase);
            name = name.Replace("_Layer_1", string.Empty, StringComparison.OrdinalIgnoreCase);
            return name;
        }

        private static string BuildSpriteName(string designName, int row, int column)
        {
            return designName + "_r" + row + "_c" + column;
        }

        private static string ToStableId(string designName)
        {
            var value = (designName ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');
            return "modern.home-designs." + value;
        }

        private static string ToDisplayName(string designName)
        {
            return string.Join(" ", (designName ?? string.Empty).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string CombineAssetPath(string left, string right)
        {
            return NormalizeAssetPath((left ?? string.Empty).TrimEnd('/') + "/" + (right ?? string.Empty).Trim('/'));
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            folderPath = NormalizeAssetPath(folderPath);
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parts = folderPath.Split('/');
            var current = parts[0];
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
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string NormalizeAssetPath(string path)
        {
            var normalized = Normalize(path);
            var index = normalized.IndexOf("Assets/", StringComparison.Ordinal);
            return index >= 0 ? normalized.Substring(index) : normalized;
        }
    }
}
