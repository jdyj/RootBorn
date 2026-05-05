using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class PixelwoodSliceSetup
    {
        private const int CellSize = 16;
        // Pixelwood Player Character: 236x49 (4프레임 × 59x49) — SlimeMaster 분석 보고서 기준
        private const int CharCellW = 59;
        private const int CharCellH = 49;

        private struct SliceTarget
        {
            public string AssetPath;
            public int CellW;
            public int CellH;
            public bool SingleRow;
            public string LabelPrefix;
            public int PixelsPerUnit; // 0이면 16 기본
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
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley Icon Pack 1.0/1.0/Items 16x16.png",
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
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Idle_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Idle_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Idle_Up", PixelsPerUnit = CharCellH
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Down.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Walk_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Walk_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Walk_Up", PixelsPerUnit = CharCellH
            },
            // 도구 장착 모션 — Axe/Hoe/Pickaxe/Pickup × Down/Side/Up. 모두 59x49 cell, PPU 49.
            // Frame 수는 sheet 마다 다름 (Axe/Pickaxe = 6f, Hoe/Down = 7f, Pickup = 3f). SliceOne 가 width/CellW 로 자동 계산.
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Axe/Down.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Axe_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Axe/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Axe_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Axe/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Axe_Up", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Hoe/Down.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Hoe_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Hoe/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Hoe_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Hoe/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Hoe_Up", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/pickaxe/Down.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickaxe_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/pickaxe/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickaxe_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/pickaxe/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickaxe_Up", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Pickup/Down.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickup_Down", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Pickup/Side.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickup_Side", PixelsPerUnit = CharCellH
            },
            new SliceTarget {
                AssetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Pickup/Up.png",
                CellW = CharCellW, CellH = CharCellH, SingleRow = true, LabelPrefix = "Pickup_Up", PixelsPerUnit = CharCellH
            },
            // Fantasy Book UI V2 — Icons sheet (224x80 = 14x5 cells, 16x16 each).
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Buttons & Icons/Icons 16x16.png",
                CellW = CellSize, CellH = CellSize, LabelPrefix = "BookIcon"
            },
            // Fantasy Book UI V2 — Index sheet (64x64 = 4x4 cells, 16x16 each).
            new SliceTarget
            {
                AssetPath = "Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Buttons & Icons/Index 16x16.png",
                CellW = CellSize, CellH = CellSize, LabelPrefix = "BookIndex"
            },
            // Fantasy Book UI V2 — Bookmark sheet (별도 SliceBookmarkSheet 메서드 사용, Targets 에서 제외).
        };

        [MenuItem("Rootborn/Pixelwood/Slice Sprite Sheets")]
        public static void SliceAll()
        {
            int sliced = 0;
            foreach (var t in Targets)
            {
                if (SliceOne(t)) sliced++;
            }
            // Bookmark sheet — 22×99 가 5×19 셀 균등 분할이 아니라 (전체 99 / 5 = 19.8), 명시 rect 으로 슬라이스.
            if (SliceBookmarkSheet()) sliced++;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Pixelwood slice complete. {sliced} sheets processed.");
        }

        // Bookmark sheet (22×99) — 5색을 위→아래로 명시 rect 슬라이스. 마지막 셀은 21px 로 약간 큼 (잔여 픽셀 흡수).
        private static bool SliceBookmarkSheet()
        {
            const string assetPath = "Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Bookmarks/1 22x20.png";
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return false;

            // 사용자 지정 rect — Unity sprite rect 는 left-bottom origin.
            // (yBottom, height) 쌍, 모두 width=22:
            //   Bookmark_0: y=79, h=20  (가장 위)
            //   Bookmark_1: y=59, h=20
            //   Bookmark_2: y=39, h=20
            //   Bookmark_3: y=19, h=20
            //   Bookmark_4: y=0,  h=19  (가장 아래)
            int W = tex.width; // 22
            var rects = new (int yBottom, int height)[]
            {
                (79, 20),
                (59, 20),
                (39, 20),
                (19, 20),
                ( 0, 19),
            };
            var metas = new List<SpriteMetaData>();
            for (int i = 0; i < rects.Length; i++)
            {
                var (yBottom, h) = rects[i];
                metas.Add(new SpriteMetaData
                {
                    name = $"Bookmark_{i}",
                    rect = new Rect(0, yBottom, W, h),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                });
            }
#pragma warning disable CS0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[ROOTBORN] Bookmark sheet sliced: 5 sub-sprites (Bookmark_0..4) {W}x{tex.height}");
            return true;
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
            importer.spritePixelsPerUnit = target.PixelsPerUnit > 0 ? target.PixelsPerUnit : 16;
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

        /// <summary>
        /// 9-slice border 와 single-sprite 임포트 일괄 설정. ConfigureFantasyBookUI 가 호출.
        /// </summary>
        public static bool ConfigureSingleSprite(string assetPath, Vector4 border, int pixelsPerUnit = 16)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = border;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        [MenuItem("Rootborn/Pixelwood/Configure Fantasy Book UI 9-slice")]
        public static void ConfigureFantasyBookUI()
        {
            int ok = 0;

            // Sizeable Boxes — 모두 16x16 또는 20x20 픽셀, border 4px (네 모서리 4px 안쪽 stretch).
            for (int i = 1; i <= 71; i++)
            {
                var p = $"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Sizeable Boxes/{i}.png";
                if (ConfigureSingleSprite(p, new Vector4(4, 4, 4, 4))) ok++;
            }

            // Icon Container — 슬롯 테두리, border 6px.
            for (int i = 1; i <= 12; i++)
            {
                var p = $"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Unique/Icon Container/{i}.png";
                if (ConfigureSingleSprite(p, new Vector4(6, 6, 6, 6))) ok++;
            }

            // Page + Animation — 290x184 책 sprite (외부 프레임 + 좌우 페이지 + spine 모두 통합).
            // 9-slice 불가 (모서리 회색 장식이 박혀있어 늘리면 깨짐) → border 0, Simple + preserveAspect.
            for (int i = 1; i <= 9; i++)
            {
                var p = $"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Page + Animation/Page{i}.png";
                if (ConfigureSingleSprite(p, Vector4.zero)) ok++;
            }

            // Titles — 리본은 stretch 안 함, single 사용.
            for (int i = 1; i <= 30; i++)
            {
                var p = $"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Titles/{i}.png";
                if (ConfigureSingleSprite(p, Vector4.zero)) ok++;
            }

            // Bookmarks 는 sliced multi-sprite (PixelwoodSliceSetup.SliceAll 가 처리) — 여기서 설정 안 함.
            ConfigureSingleSprite("Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Unique/DarkerPage.png", new Vector4(20, 20, 20, 20));
            ConfigureSingleSprite("Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Unique/Character.png", Vector4.zero);
            for (int i = 1; i <= 3; i++)
            {
                ConfigureSingleSprite($"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Unique/Box1.{i}.png", Vector4.zero);
            }

            // Fancy Inscriptions — Cutter / Plus / Triangle / Inscriptions sheets
            ConfigureSingleSprite("Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Fancy Inscriptions/Cutter 16x16.png", Vector4.zero);
            ConfigureSingleSprite("Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Fancy Inscriptions/Plus.PNG", Vector4.zero);
            ConfigureSingleSprite("Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Fancy Inscriptions/Trinagle.png", Vector4.zero);
            for (int i = 1; i <= 4; i++)
            {
                ConfigureSingleSprite($"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Fancy Inscriptions/{i}.1.png", Vector4.zero);
                ConfigureSingleSprite($"Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites/Fancy Inscriptions/{i}.2.png", Vector4.zero);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Fantasy Book UI 9-slice configured: {ok} sprites.");
        }
    }
}
