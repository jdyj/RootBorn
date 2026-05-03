using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class PixelwoodSliceSetup
    {
        private const int CellSize = 16;

        private struct SliceTarget
        {
            public string AssetPath;
            public int CellW;
            public int CellH;
            public bool SingleRow;
            public string LabelPrefix;
        }

        private static readonly SliceTarget[] Targets = new[]
        {
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Farm/Crops/crops 16x16.png",
                CellW = CellSize, CellH = CellSize, LabelPrefix = "Crop"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley Icon Pack 1.0/1.0/Items 16x16.png",
                CellW = CellSize, CellH = CellSize, LabelPrefix = "Icon"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Tiles/Tile.png",
                CellW = CellSize, CellH = CellSize, LabelPrefix = "Tile"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Down.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Idle_Down"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Side.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Idle_Side"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Up.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Idle_Up"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Down.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Walk_Down"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Side.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Walk_Side"
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Up.png",
                CellW = CellSize, CellH = CellSize, SingleRow = true, LabelPrefix = "Walk_Up"
            }
        };

        [MenuItem("Rootborn/Pixelwood/Slice Sprite Sheets")]
        public static void SliceAll()
        {
            int sliced = 0;
            foreach (var t in Targets)
            {
                if (SliceOne(t)) sliced++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Pixelwood slice complete. {sliced}/{Targets.Length} sheets processed.");
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
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;

            // First reimport so width/height are accurate before slicing.
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(target.AssetPath);
            if (tex == null)
            {
                Debug.LogWarning($"[ROOTBORN] failed to load texture: {target.AssetPath}");
                return false;
            }

            int cols = Mathf.Max(1, tex.width / target.CellW);
            int rows = target.SingleRow ? 1 : Mathf.Max(1, tex.height / target.CellH);

            var metas = new List<SpriteMetaData>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int x = col * target.CellW;
                    int yFromTop = row * target.CellH;
                    int yFromBottom = tex.height - target.CellH - yFromTop;
                    if (yFromBottom < 0) continue;

                    string name = target.SingleRow
                        ? $"{target.LabelPrefix}_{col}"
                        : $"{target.LabelPrefix}_r{row}_c{col}";

                    metas.Add(new SpriteMetaData
                    {
                        name = name,
                        rect = new Rect(x, yFromBottom, target.CellW, target.CellH),
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

            Debug.Log($"[ROOTBORN] Sliced {target.AssetPath} → {metas.Count} sprites ({cols}x{rows})");
            return true;
        }

        public static IEnumerable<Sprite> LoadAllSubSprites(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var a in assets)
            {
                if (a is Sprite s) yield return s;
            }
        }
    }
}
