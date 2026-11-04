using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.UI.StudentLife;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class SchoolDesignComfyAssetTests
    {
        const string ScenePath = "Assets/Scenes/School.unity";
        const string SpriteRoot = "Assets/Art/SchoolPixels/Comfy31/School_wall_";
        const string TileRoot = "Assets/Data/Interiors/TileSets/SchoolComfy31/School_Wall";
        const string BorderSpriteRoot = "Assets/Art/SchoolPixels/Comfy31/School_border_";
        const string BorderTileRoot = "Assets/Data/Interiors/TileSets/SchoolComfy31/School_Border";
        const string PromptField = "_interactionPromptOffset";
        static readonly string[] BorderRoles = { "left_cap", "middle_cap", "right_cap", "left_strip", "middle_strip", "right_strip" };
        static readonly string[] BorderTitles = { "LeftCap", "MiddleCap", "RightCap", "LeftStrip", "MiddleStrip", "RightStrip" };
        static readonly string[] BorderOriginalGuids = { "10a9b9cc4178bb8439f0014047c94c84", "850084223cc284341a61c4c6be1c3d38", "0f9ec2b359c0398448355799abbd4e35", "868c4842b72b8a341ac74915f547df1c", "0d5a2fc4527ca0444ba23176f8d7737a", "238bee1e280035649bae24603dbfa609" };
        static readonly string[] WallTitles = { "Top", "Bottom", "CornerTop", "CornerBottom" };
        static readonly string[] WallAddressRoles = { "top", "bottom", "corner-top", "corner-bottom" };
        static readonly string[] WallOriginalGuids = { "98dfeb66d49bc5042927928c4659975d", "0d6df23c3ae9eb3418c5302d13a17aa4", "13eb72da0a8ca054399273ce0831eff2", "d3bc52b203fdaad4fb0cd389554b8636" };
        static readonly int[] BorderCounts = { 1, 1, 1, 15, 10, 15 };

        [TestCase("top", 14)]
        [TestCase("bottom", 26)]
        public void DedicatedSpriteUsesOpaqueTwoPixelClustersAndOneWorldCellImport(string role, int colors)
        {
            string path = SpriteRoot + role + "_48.png";
            var sprite = Required<Sprite>(path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 48, 48)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(24, 24)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            CheckAddress(path, "sprites/school/comfy31/wall-" + role, "Sprites");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(48, 48)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 255), Is.True, "Every wall pixel must cover its cell");
                Assert.That(new HashSet<uint>(pixels.Select(Key)).Count, Is.EqualTo(colors));
                for (int y = 0; y < 48; y += 2)
                    for (int x = 0; x < 48; x += 2)
                        Assert.That(new[] { Key(pixels[y * 48 + x + 1]), Key(pixels[(y + 1) * 48 + x]), Key(pixels[(y + 1) * 48 + x + 1]) },
                            Is.All.EqualTo(Key(pixels[y * 48 + x])), "Nonuniform 2x2 at " + x + "," + y);
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void FourOwnedWallRolesPreserveOriginalTileContractsWithBothCornersUsingTopPixels()
        {
            for (int i = 0; i < 4; i++)
            {
                string path = TileRoot + WallTitles[i] + ".asset";
                var tile = Required<Tile>(path);
                var original = Required<Tile>(AssetDatabase.GUIDToAssetPath(WallOriginalGuids[i]));
                Assert.That(tile, Is.Not.SameAs(original));
                Assert.That(tile.sprite, Is.SameAs(Required<Sprite>(SpriteRoot + (i == 1 ? "bottom" : "top") + "_48.png")));
                CheckTileContract(tile, original);
                Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None));
                CheckAddress(path, "tiles/school/comfy31/wall-" + WallAddressRoles[i], "Tiles");
            }
        }

        [Test]
        public void SixBorderSpritesKeepOneCellGeometryTwoPixelClustersAndEveryOriginalAlphaSample()
        {
            for (int i = 0; i < 6; i++)
            {
                string path = BorderSpriteRoot + BorderRoles[i] + "_32.png";
                var sprite = Required<Sprite>(path);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 32, 32)), BorderRoles[i]);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32f));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(16, 16)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                CheckAddress(path, "sprites/school/comfy31/border-" + BorderRoles[i].Replace('_', '-'), "Sprites");
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                    Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(32, 32)));
                    var pixels = raw.GetPixels32();
                    Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                    Assert.That(pixels.Where(p => p.a != 0).Select(Key).Distinct().Count(), Is.EqualTo(i < 3 ? 10 : 8));
                    for (int y = 0; y < 32; y += 2) for (int x = 0; x < 32; x += 2)
                        Assert.That(new[] { Key(pixels[y * 32 + x + 1]), Key(pixels[(y + 1) * 32 + x]), Key(pixels[(y + 1) * 32 + x + 1]) }, Is.All.EqualTo(Key(pixels[y * 32 + x])));
                    var original = Required<Tile>(AssetDatabase.GUIDToAssetPath(BorderOriginalGuids[i])).sprite;
                    Assert.That(pixels.Select(p => p.a).ToArray(), Is.EqualTo(ReadSpriteAlpha(original)), "All 1024 original alpha samples: " + BorderRoles[i]);
                    CheckPhysicsShapes(sprite, original);
                }
                finally { Object.DestroyImmediate(raw); }
            }
        }

        [Test]
        public void SixOwnedBorderTilesRetainOriginalColliderFlagsMatrixColorAndPrefab()
        {
            for (int i = 0; i < 6; i++)
            {
                string path = BorderTileRoot + BorderTitles[i] + ".asset";
                var tile = Required<Tile>(path);
                var original = Required<Tile>(AssetDatabase.GUIDToAssetPath(BorderOriginalGuids[i]));
                Assert.That(tile, Is.Not.SameAs(original));
                Assert.That(tile.sprite, Is.SameAs(Required<Sprite>(BorderSpriteRoot + BorderRoles[i] + "_32.png")));
                CheckTileContract(tile, original);
                Assert.That((int)tile.colliderType, Is.EqualTo(i == 1 ? 0 : 1), BorderRoles[i]);
                CheckAddress(path, "tiles/school/comfy31/border-" + BorderRoles[i].Replace('_', '-'), "Tiles");
            }
        }

        [Test]
        public void SavedWallReferencesAllFourOwnedRolesAndTheirMatchingSpriteCaches()
        {
            string wall = Block(ReadScene(), 1364229726);
            var tiles = References(wall, "m_TileAssetArray", "m_TileSpriteArray");
            var sprites = References(wall, "m_TileSpriteArray", "m_TileMatrixArray");
            Assert.That(tiles.Length, Is.EqualTo(4)); Assert.That(sprites.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                int count = i < 2 ? 39 : 1;
                CheckReference(tiles[i], Required<Tile>(TileRoot + WallTitles[i] + ".asset"), count);
                CheckReference(sprites[i], Required<Sprite>(SpriteRoot + (i == 1 ? "bottom" : "top") + "_48.png"), count);
            }
        }

        [Test]
        public void SavedBorderReferencesSixOwnedRolesAndTheirMatchingSpriteCaches()
        {
            string border = Block(ReadScene(), 1334193329);
            var tiles = References(border, "m_TileAssetArray", "m_TileSpriteArray");
            var sprites = References(border, "m_TileSpriteArray", "m_TileMatrixArray");
            Assert.That(tiles.Length, Is.EqualTo(6)); Assert.That(sprites.Length, Is.EqualTo(6));
            for (int i = 0; i < 6; i++)
            {
                CheckReference(tiles[i], Required<Tile>(BorderTileRoot + BorderTitles[i] + ".asset"), BorderCounts[i]);
                CheckReference(sprites[i], Required<Sprite>(BorderSpriteRoot + BorderRoles[i] + "_32.png"), BorderCounts[i]);
            }
        }

        [Test]
        public void SavedSchoolRetainsThirtyNineAdjacentPairsTwoCornerCellsAndStructuralBounds()
        {
            // Serialized inspection avoids loading a scene or disturbing host lighting.
            string source = ReadScene();
            string wall = Block(source, 1364229726);
            var cells = Regex.Matches(Section(wall, "m_Tiles", "m_TileAssetArray"),
                @"- first: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}\n(.*?)(?=  - first:|\z)", RegexOptions.Singleline).Cast<Match>().ToArray();
            Assert.That(cells.Length, Is.EqualTo(80));
            var actual = new Dictionary<Vector3Int, int>();
            foreach (var cell in cells)
            {
                var position = new Vector3Int(int.Parse(cell.Groups[1].Value), int.Parse(cell.Groups[2].Value), int.Parse(cell.Groups[3].Value));
                string values = cell.Groups[4].Value;
                int index = int.Parse(Regex.Match(values, @"m_TileIndex: (\d+)").Groups[1].Value);
                Assert.That(actual.ContainsKey(position), Is.False, "Duplicate saved cell");
                actual.Add(position, index);
                Assert.That(values, Does.Contain("m_TileSpriteIndex: " + index + "\n"));
                Assert.That(values, Does.Contain("m_TileMatrixIndex: 0\n"));
                Assert.That(values, Does.Contain("m_TileColorIndex: 0\n"));
                Assert.That(values, Does.Contain("m_TileObjectToInstantiateIndex: 65535\n"));
                Assert.That(values, Does.Contain("m_AllTileFlags: 1\n"));
            }
            var expected = new Dictionary<Vector3Int, int>();
            foreach (int x in Enumerable.Range(1, 21)) AddPair(expected, x, -2);
            foreach (int x in new[] { 1 }.Concat(Enumerable.Range(4, 7)).Concat(Enumerable.Range(12, 10))) AddPair(expected, x, -13);
            expected.Add(new Vector3Int(11, -12, 0), 2);
            expected.Add(new Vector3Int(11, -13, 0), 3);
            Assert.That(actual.Count(p => p.Value == 0), Is.EqualTo(39));
            Assert.That(actual.Count(p => p.Value == 1), Is.EqualTo(39));
            foreach (var pair in expected)
            {
                Assert.That(actual.ContainsKey(pair.Key), Is.True, "Missing authored cell " + pair.Key);
                Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), "Wrong cell role at " + pair.Key);
            }
            Assert.That(wall, Does.Contain("m_Origin: {x: 0, y: -13, z: 0}"));
            Assert.That(wall, Does.Contain("m_Size: {x: 22, y: 13, z: 1}"));
            Assert.That(wall, Does.Contain("m_TileAnchor: {x: 0.5, y: 0.5, z: 0}"));
            string transform = Block(source, 1364229724);
            Assert.That(transform, Does.Contain("m_LocalPosition: {x: 0, y: 0, z: 0}"));
            Assert.That(transform, Does.Contain("m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}"));
            Assert.That(transform, Does.Contain("m_LocalScale: {x: 1, y: 1, z: 1}"));
            Assert.That(Regex.Matches(Section(Block(source, 1334193329), "m_Tiles", "m_TileAssetArray"), @"- first:").Count, Is.EqualTo(43));
            Assert.That(Regex.Matches(Section(Block(source, 1204997190), "m_Tiles", "m_TileAssetArray"), @"- first:").Count, Is.EqualTo(304));
            Assert.That(Regex.Matches(source, @"^--- !u!61 &", RegexOptions.Multiline).Count, Is.EqualTo(139), "All scene boxes remain authored");
            var borderCells = Regex.Matches(Section(Block(source, 1334193329), "m_Tiles", "m_TileAssetArray"), @"- first: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}\n(.*?)(?=  - first:|\z)", RegexOptions.Singleline).Cast<Match>().ToArray();
            var expectedBorder = new Dictionary<Vector3Int, int>();
            expectedBorder.Add(new Vector3Int(0, -16, 0), 3);
            expectedBorder.Add(new Vector3Int(22, -16, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -15, 0), 3);
            expectedBorder.Add(new Vector3Int(22, -15, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -14, 0), 3);
            expectedBorder.Add(new Vector3Int(22, -14, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -13, 0), 3);
            expectedBorder.Add(new Vector3Int(22, -13, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -12, 0), 3);
            expectedBorder.Add(new Vector3Int(22, -12, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -11, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -11, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -11, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -10, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -10, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -10, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -9, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -9, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -9, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -8, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -8, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -8, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -7, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -7, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -7, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -6, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -6, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -6, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -5, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -5, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -5, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -4, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -4, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -4, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -3, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -3, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -3, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -2, 0), 3);
            expectedBorder.Add(new Vector3Int(11, -2, 0), 4);
            expectedBorder.Add(new Vector3Int(22, -2, 0), 5);
            expectedBorder.Add(new Vector3Int(0, -1, 0), 0);
            expectedBorder.Add(new Vector3Int(11, -1, 0), 1);
            expectedBorder.Add(new Vector3Int(22, -1, 0), 2);
            foreach (var cell in borderCells)
            {
                var point = new Vector3Int(int.Parse(cell.Groups[1].Value), int.Parse(cell.Groups[2].Value), int.Parse(cell.Groups[3].Value));
                Assert.That(expectedBorder.ContainsKey(point), Is.True, "Unexpected authored border cell " + point);
                int index = expectedBorder[point]; string values = cell.Groups[4].Value;
                Assert.That(values, Does.Contain("m_TileIndex: " + index + "\n"));
                Assert.That(values, Does.Contain("m_TileSpriteIndex: " + index + "\n"));
                Assert.That(values, Does.Contain("m_TileMatrixIndex: 0\n")); Assert.That(values, Does.Contain("m_TileColorIndex: 0\n"));
                Assert.That(values, Does.Contain("m_AllTileFlags: " + (index == 1 ? "1" : "1073741825") + "\n"));
            }
            Assert.That(Regex.Matches(source, @"^--- !u!19719996 &", RegexOptions.Multiline).Count, Is.Zero, "School has no TilemapCollider2D");
            Assert.That(Regex.Matches(source, @"^--- !u!66 &", RegexOptions.Multiline).Count, Is.Zero, "School has no CompositeCollider2D");
        }

        [TestCase(714, 8f)]
        [TestCase(540, 4f)]
        public void SchoolPromptEnvelopeClearsOpaqueNoticeboardAtBothSupportedHeights(int height, float requiredGap)
        {
            var go = new GameObject("OwnedSchoolPromptEnvelopeTest");
            try
            {
                string scene = ReadScene(); var board = go.AddComponent<DailyPlanBoardInteractor>();
                ApplyOffset(board, Block(scene, 224293929));
                Vector3 offset = board.InteractionPromptOffset;
                Assert.That(offset.x, Is.Zero); Assert.That(offset.z, Is.Zero);
                float rootY = Vector(Block(scene, 224293928), "m_LocalPosition").y;
                Assert.That(rootY, Is.EqualTo(-2f));
                var sprite = Required<Sprite>("Assets/Art/SchoolPixels/sprites/noticeboard.png");
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                float top;
                try
                {
                    Assert.That(raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))), Is.True);
                    var pixels = raw.GetPixels32();
                    int topPixel = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).Max(i => i / raw.width) + 1;
                    string visual = Block(scene, 994874734);
                    Assert.That(Vector(visual, "m_LocalScale"), Is.EqualTo(Vector3.one));
                    top = rootY + Vector(visual, "m_LocalPosition").y + (topPixel - sprite.pivot.y) / sprite.pixelsPerUnit;
                }
                finally { Object.DestroyImmediate(raw); }
                // Actual native baseline uses orthographicSize5.5 and a 24px fitted prompt.
                // Native first/cold captures must separately verify actual glyph bounds/HUD.
                float gap = (rootY + offset.y - top) * height / 11f - 12f;
                float topMargin = height - ((rootY + offset.y + 5.5f) * height / 11f + height * .5f + 12f);
                Assert.That(gap, Is.GreaterThanOrEqualTo(requiredGap), "School-only prompt overlaps the noticeboard; do not move its art or change House/default offsets.");
                Assert.That(topMargin, Is.GreaterThanOrEqualTo(4f), "Prompt envelope must remain inside the actual overview viewport.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SavedSchoolOwnsOnlyOnePointSixPromptOverride()
        {
            string scene = ReadScene();
            Assert.That(Regex.Matches(scene, @"(?m)^  " + PromptField + ":").Count, Is.EqualTo(1));
            Assert.That(Vector(Block(scene, 224293929), PromptField), Is.EqualTo(new Vector3(0, 1.6f, 0)));
            var go = new GameObject("OwnedSchoolAuthoredPromptTest");
            try { var board = go.AddComponent<DailyPlanBoardInteractor>(); ApplyOffset(board, Block(scene, 224293929)); Assert.That(board.InteractionPromptOffset, Is.EqualTo(new Vector3(0, 1.6f, 0))); }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HouseOnePointSevenAndUnconfiguredBoardContractRemainUnchanged()
        {
            Assert.That(Vector(Block(File.ReadAllText("Assets/Scenes/House.unity").Replace("\r\n", "\n"), 109339619), PromptField), Is.EqualTo(new Vector3(0, 1.7f, 0)));
            var go = new GameObject("OwnedSchoolDefaultPromptTest");
            try
            {
                var board = go.AddComponent<DailyPlanBoardInteractor>();
                Assert.That(board.InteractionPromptOffset, Is.EqualTo(new Vector3(0, 1.1f, 0)));
                Assert.That(board.InteractionPriority, Is.EqualTo(80)); Assert.That(board.InteractionPrompt, Is.EqualTo("[E] Plan Day"));
                Assert.That(board.InteractionTransform, Is.SameAs(go.transform));
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void ApplyOffset(DailyPlanBoardInteractor board, string body)
        {
            if (!Regex.IsMatch(body, @"(?m)^  " + PromptField + ":")) return;
            var serialized = new SerializedObject(board); var field = serialized.FindProperty(PromptField);
            Assert.That(field, Is.Not.Null); field.vector3Value = Vector(body, PromptField); serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static Vector3 Vector(string body, string name)
        {
            var match = Regex.Match(body, @"(?m)^  " + Regex.Escape(name) + @": \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");
            Assert.That(match.Success, Is.True, "Missing authored vector " + name);
            return new Vector3(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }
        static void CheckTileContract(Tile tile, Tile original)
        {
            Assert.That(tile.GetType(), Is.EqualTo(typeof(Tile))); Assert.That(tile.transform, Is.EqualTo(original.transform));
            Assert.That(tile.color, Is.EqualTo(original.color)); Assert.That(tile.flags, Is.EqualTo(original.flags));
            Assert.That(tile.colliderType, Is.EqualTo(original.colliderType)); CheckPrefabReference(tile, original);
        }
        static void CheckPrefabReference(Tile actual, Tile original)
        {
            // Unity can return distinct managed wrappers for the same null native object.
            // Compare Unity identity AND the persisted prefab reference; never relax to managed-null alone.
            Assert.That(actual.gameObject == original.gameObject, Is.True, "Original prefab Unity identity must be preserved");
            var actualProperty = new SerializedObject(actual).FindProperty("m_InstancedGameObject");
            var originalProperty = new SerializedObject(original).FindProperty("m_InstancedGameObject");
            Assert.That(actualProperty, Is.Not.Null); Assert.That(originalProperty, Is.Not.Null);
            Assert.That(actualProperty.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
            Assert.That(originalProperty.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
            Assert.That(actualProperty.objectReferenceInstanceIDValue, Is.EqualTo(originalProperty.objectReferenceInstanceIDValue));
            string actualField = PrefabReferenceField(actual), originalField = PrefabReferenceField(original);
            Assert.That(actualField, Is.EqualTo(originalField), "Saved prefab fileID/GUID/type must remain exact");
            if (originalProperty.objectReferenceInstanceIDValue == 0)
            {
                Assert.That(originalField, Is.EqualTo("{fileID: 0}"), "Do not hide a missing nonzero prefab reference");
                Assert.That(actual.gameObject == null && original.gameObject == null, Is.True);
                return;
            }
            Assert.That(actual.gameObject != null && original.gameObject != null, Is.True, "Nonzero prefab references must resolve");
            string actualGuid, originalGuid; long actualId, originalId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actual.gameObject, out actualGuid, out actualId), Is.True);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original.gameObject, out originalGuid, out originalId), Is.True);
            Assert.That(actualGuid, Is.EqualTo(originalGuid)); Assert.That(actualId, Is.EqualTo(originalId));
            string originalPath = AssetDatabase.GetAssetPath(original.gameObject);
            Assert.That(originalPath, Is.Not.Empty);
            Assert.That(AssetDatabase.GetAssetPath(actual.gameObject), Is.EqualTo(originalPath));
        }
        static string PrefabReferenceField(Tile tile)
        {
            var matches = Regex.Matches(File.ReadAllText(AssetDatabase.GetAssetPath(tile)), @"(?m)^  m_InstancedGameObject: (\{[^}\r\n]+\})\r?$");
            Assert.That(matches.Count, Is.EqualTo(1), "Exactly one saved prefab reference required");
            return matches[0].Groups[1].Value;
        }
        static void CheckPhysicsShapes(Sprite actual, Sprite original)
        {
            Assert.That(actual.GetPhysicsShapeCount(), Is.EqualTo(original.GetPhysicsShapeCount()), "Preserve imported original border physics shape count");
            for (int i = 0; i < original.GetPhysicsShapeCount(); i++)
            {
                var a = new List<Vector2>(); var b = new List<Vector2>(); actual.GetPhysicsShape(i, a); original.GetPhysicsShape(i, b);
                Assert.That(a, Is.EqualTo(b), "Preserve every original physics vertex/order");
            }
        }
        static byte[] ReadSpriteAlpha(Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null); Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(32, 32)));
            var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prior = RenderTexture.active; Texture2D read = null;
            try
            {
                Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt; read = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
                read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); read.Apply(); var pixels = read.GetPixels32(); var alpha = new byte[1024];
                int x0 = Mathf.RoundToInt(sprite.rect.x), y0 = Mathf.RoundToInt(sprite.rect.y);
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) alpha[y * 32 + x] = pixels[(y0 + y) * rt.width + x0 + x].a;
                return alpha;
            }
            finally { RenderTexture.active = prior; if (read != null) Object.DestroyImmediate(read); RenderTexture.ReleaseTemporary(rt); }
        }

        static void AddPair(Dictionary<Vector3Int, int> cells, int x, int y)
        {
            cells.Add(new Vector3Int(x, y, 0), 1);
            cells.Add(new Vector3Int(x, y + 1, 0), 0);
        }
        static string ReadScene() => File.ReadAllText(ScenePath).Replace("\r\n", "\n");
        static string Block(string source, long fileId)
        {
            var matches = Regex.Matches(source, @"^--- !u!\d+ &" + fileId + @"\n.*?(?=^--- !u!|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one serialized object " + fileId);
            return matches[0].Value;
        }
        static string Section(string source, string start, string end)
        {
            string first = "  " + start + ":";
            string last = "  " + end + ":";
            int a = source.IndexOf(first, StringComparison.Ordinal), b = source.IndexOf(last, StringComparison.Ordinal);
            Assert.That(a, Is.GreaterThanOrEqualTo(0), start);
            Assert.That(b, Is.GreaterThan(a), end);
            return source.Substring(a + first.Length, b - a - first.Length);
        }
        static Match[] References(string source, string start, string end) => Regex.Matches(Section(source, start, end),
            @"- m_RefCount: (\d+)\n    m_Data: \{fileID: (-?\d+), guid: ([a-f0-9]+), type: (\d+)\}").Cast<Match>().ToArray();
        static void CheckReference(Match entry, Object value, int count)
        {
            string guid; long fileId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out fileId), Is.True);
            CheckLiteralReference(entry, guid, fileId.ToString(System.Globalization.CultureInfo.InvariantCulture), count, value is Sprite ? "3" : "2");
        }
        static void CheckLiteralReference(Match entry, string guid, string fileId, int count, string referenceType = "2")
        {
            Assert.That(entry.Groups[1].Value, Is.EqualTo(count.ToString()));
            Assert.That(entry.Groups[2].Value, Is.EqualTo(fileId));
            Assert.That(entry.Groups[3].Value, Is.EqualTo(guid));
            Assert.That(entry.Groups[4].Value, Is.EqualTo(referenceType));
        }
        static void CheckAddress(string path, string address, string group)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, address);
            Assert.That(entry.address, Is.EqualTo(address));
            Assert.That(entry.parentGroup.Name, Is.EqualTo(group));
            Assert.That(entry.labels, Does.Contain("SchoolComfy31"));
            Assert.That(entry.labels, Does.Contain("SchoolPixels"));
            Assert.That(settings.groups.Where(g => g != null).SelectMany(g => g.entries).Count(e => e.address == address), Is.EqualTo(1));
        }
        static T Required<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null, "Missing dedicated asset: " + path);
            return value;
        }
        static uint Key(Color32 p) => (uint)(p.r | p.g << 8 | p.b << 16 | p.a << 24);
    }
}
