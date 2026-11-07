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
        };

        public readonly struct ExplicitRect
        {
            public ExplicitRect(string name, int x, int yFromTop, int width, int height)
            {
                Name = name;
                X = x;
                YFromTop = yFromTop;
                Width = width;
                Height = height;
            }

            public string Name { get; }
            public int X { get; }
            public int YFromTop { get; }
            public int Width { get; }
            public int Height { get; }
        }

        private const string Style2Path = "Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png";

        // Sprites that are not aligned to the 16px grid, cut by their real bounds (PNG top-left origin).
        public static readonly IReadOnlyList<ExplicitRect> Style2ExplicitRects = new[]
        {
            new ExplicitRect("ModernUI_16_Style2_tab_box", 61, 13, 22, 22),
        };

        private static IReadOnlyList<ExplicitRect> ExplicitRectsFor(SliceTarget target)
        {
            return target.AssetPath == Style2Path ? Style2ExplicitRects : System.Array.Empty<ExplicitRect>();
        }

        private static SpriteMetaData ToMeta(ExplicitRect rect, int textureHeight)
        {
            return new SpriteMetaData
            {
                name = rect.Name,
                rect = new Rect(rect.X, textureHeight - rect.YFromTop - rect.Height, rect.Width, rect.Height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero
            };
        }

        // Adds the explicit-rect sprites to the already sliced sheets without rebuilding the grid,
        // so existing sub-sprite names and IDs stay as they are. Batchmode: -executeMethod
        // Rootborn.Editor.Tools.ModernUiSliceSetup.AddExplicitRects
        [MenuItem("Rootborn/Modern UI/Add Explicit Rect Sprites")]
        public static void AddExplicitRects()
        {
            for (int i = 0; i < Targets.Count; i++)
            {
                var target = Targets[i];
                var extras = ExplicitRectsFor(target);
                if (extras.Count == 0) continue;

                var importer = AssetImporter.GetAtPath(target.AssetPath) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target.AssetPath);
                if (importer == null || texture == null)
                {
                    Debug.LogWarning($"[ROOTBORN] cannot add explicit rects: {target.AssetPath}");
                    continue;
                }

#pragma warning disable CS0618
                var metas = new List<SpriteMetaData>(importer.spritesheet);
#pragma warning restore CS0618
                int added = 0;
                for (int e = 0; e < extras.Count; e++)
                {
                    var meta = ToMeta(extras[e], texture.height);
                    int existing = metas.FindIndex(m => m.name == meta.name);
                    if (existing >= 0)
                    {
                        if (metas[existing].rect == meta.rect) continue;
                        metas[existing] = meta;
                    }
                    else
                    {
                        metas.Add(meta);
                    }

                    added++;
                }

                if (added == 0) continue;
#pragma warning disable CS0618
                importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"[ROOTBORN] Added {added} explicit rect sprites to {target.AssetPath}");
            }

            AssetDatabase.SaveAssets();
        }

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

            var extras = ExplicitRectsFor(target);
            for (int e = 0; e < extras.Count; e++)
            {
                metas.Add(ToMeta(extras[e], texture.height));
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
