using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernInteriorsSliceSetup
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
            new SliceTarget("Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Floors_16x16.png", 16, "ModernInterior_Floor", 15, 40),
            new SliceTarget("Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Walls_16x16.png", 16, "ModernInterior_Wall", 32, 40),
            new SliceTarget("Assets/moderninteriors-win/4_User_Interface_Elements/UI_16x16.png", 16, "ModernInterior_UI", 18, 16),
        };

        [MenuItem("Rootborn/Modern Interiors/Slice Core 16x16 Sheets")]
        public static void SliceCore16()
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
            Debug.Log($"[ROOTBORN] Modern Interiors core slice complete. {sliced} sheets processed.");
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
                Debug.LogWarning($"[ROOTBORN] Modern Interiors grid mismatch: {target.AssetPath} expected {target.Columns}x{target.Rows}, actual {columns}x{rows}");
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
