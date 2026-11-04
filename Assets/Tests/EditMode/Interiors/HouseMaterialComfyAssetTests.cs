using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HouseMaterialComfyAssetTests
    {
        const string Output = "output/house-material-20261102-integration/";
        const string House = "Assets/Scenes/House.unity";
        static readonly string[] Roles = { "wall", "vertical" };
        static readonly string[] Sprites = { "Assets/Art/HousePixels/Comfy31/House31_wall_ivory_48.png", "Assets/Art/HousePixels/Comfy31/House31_wall_vertical_32.png" };
        static readonly string[] Tiles = { "Assets/Data/Interiors/TileSets/HouseComfy31/House31_wall_ivory.asset", "Assets/Data/Interiors/TileSets/HouseComfy31/House31_wall_vertical.asset" };
        static readonly string[] Sources = { "output/design-only-20261101/room-wall-tile-kit-20261102/wall_top.png", "output/design-only-20261101/room-border-238-reuse-20261102/middle_strip32.png" };
        static readonly string[] OriginalTiles = { "cb4aa470d3b8c614cad16e4ff3934f87", "004a4b8dd6784d54d8011acab1a01eee" };
        static readonly string[] OriginalSprites = { "9ef60cc67ec1d614c9d56f313a3e70ad", "fb086ad979c87ee479f4c28d4ab5dc2c" };
        static readonly string[] Addresses = { "wall-ivory", "wall-vertical" };

        [TestCase("wall", 48, 72)]
        [TestCase("vertical", 32, 48)]
        public void DedicatedSpriteRetainsApprovedPixelsWithRoleSpecificImport(string role, int size, int ppu)
        {
            int i = Array.IndexOf(Roles, role);
            var sprite = Required<Sprite>(Sprites[i]);
            Assert.That(File.ReadAllBytes(Sprites[i]), Is.EqualTo(File.ReadAllBytes(Sources[i])), "Approved PNG bytes must be copied exactly");
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, size, size)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(size / 2f, size / 2f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(ppu));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Sprites[i]);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Address(Sprites[i], "sprites/house/comfy31/" + Addresses[i], "Sprites");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(Sprites[i])), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(size, size)));
                Color32[] pixels = raw.GetPixels32(); Assert.That(pixels.All(p => p.a == 255), Is.True);
                for (int y = 0; y < size; y += 2) for (int x = 0; x < size; x += 2)
                    Assert.That(new[] { pixels[y * size + x + 1], pixels[(y + 1) * size + x], pixels[(y + 1) * size + x + 1] }, Is.All.EqualTo(pixels[y * size + x]));
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [TestCase("wall")]
        [TestCase("vertical")]
        public void OwnedTileRetainsSingleScaleGridColliderFlagsAndWorldPhysics(string role)
        {
            int i = Array.IndexOf(Roles, role);
            var tile = Required<Tile>(Tiles[i]);
            var original = Required<Tile>(AssetDatabase.GUIDToAssetPath(OriginalTiles[i]));
            Assert.That(tile == original, Is.False);
            Assert.That(tile.sprite == Required<Sprite>(Sprites[i]), Is.True);
            Assert.That(tile.transform, Is.EqualTo(original.transform));
            Assert.That(tile.transform, Is.EqualTo(Matrix4x4.Scale(new Vector3(1.5f, 1.5f, 1f))));
            Assert.That((int)tile.flags, Is.EqualTo(3)); Assert.That(tile.flags, Is.EqualTo(original.flags));
            Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.Grid));
            Assert.That(tile.colliderType, Is.EqualTo(original.colliderType));
            Assert.That(tile.color, Is.EqualTo(original.color));
            Assert.That(tile.gameObject == original.gameObject, Is.True, "Unity object equality includes null wrappers");
            Assert.That(PrefabRef(File.ReadAllText(Tiles[i])), Is.EqualTo(PrefabRef(File.ReadAllText(AssetDatabase.GetAssetPath(original)))));
            Assert.That(tile.sprite.bounds.size.x * 1.5f, Is.EqualTo(1f).Within(.000001f));
            Assert.That(tile.sprite.bounds.size.y * 1.5f, Is.EqualTo(1f).Within(.000001f));
            Assert.That(tile.sprite.GetPhysicsShapeCount(), Is.EqualTo(original.sprite.GetPhysicsShapeCount()));
            Assert.That(tile.sprite.GetPhysicsShapeCount(), Is.EqualTo(1));
            var actual = new List<Vector2>(); var old = new List<Vector2>();
            tile.sprite.GetPhysicsShape(0, actual); original.sprite.GetPhysicsShape(0, old);
            Assert.That(actual.Count, Is.EqualTo(4)); Assert.That(actual.Count, Is.EqualTo(old.Count));
            for (int p = 0; p < actual.Count; p++)
            {
                Assert.That(actual[p].x, Is.EqualTo(old[p].x).Within(.000001f));
                Assert.That(actual[p].y, Is.EqualTo(old[p].y).Within(.000001f));
            }
            Address(Tiles[i], "tiles/house/comfy31/" + Addresses[i], "Tiles");
        }

        [Test]
        public void SavedHouseUsesTwoOwnedTilesAndTwoMatchingSpriteCachesOnly()
        {
            string wall = Block(Read(House), 6396298);
            for (int i = 0; i < 2; i++)
            {
                var tile = Required<Tile>(Tiles[i]); var sprite = Required<Sprite>(Sprites[i]);
                int count = i == 0 ? 74 : 50;
                Assert.That(Section(wall, "m_TileAssetArray", "m_TileSpriteArray"), Does.Contain("- m_RefCount: " + count + "\n    m_Data: " + Ref(tile)));
                Assert.That(Section(wall, "m_TileSpriteArray", "m_TileMatrixArray"), Does.Contain("- m_RefCount: " + count + "\n    m_Data: " + Ref(sprite)));
                Assert.That(wall, Does.Not.Contain(OriginalTiles[i])); Assert.That(wall, Does.Not.Contain(OriginalSprites[i]));
            }
            Assert.That(ReversedHouse(), Is.EqualTo(File.ReadAllBytes(Output + "backup/original/" + House)), "Exactly four reference substitutions; all other bytes unchanged");
        }

        [Test]
        public void OriginalSharedDefinitionsDoorsCollisionAndEntireSceneExceptFourRefsStayExact()
        {
            string pinFile = Output + "evidence/house-material-preservation.tsv";
            Assert.That(File.Exists(pinFile), Is.True, "Parent must run guarded preparation before RED");
            string[] rows = File.ReadAllLines(pinFile).Where(x => x.Length != 0).ToArray();
            Assert.That(rows.Length, Is.EqualTo(16));
            foreach (string row in rows)
            {
                string[] fields = row.Split('\t'); Assert.That(fields.Length, Is.EqualTo(2));
                Assert.That(fields[1], Does.Match("^[0-9a-f]{64}$"));
                using (var sha = SHA256.Create()) Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(fields[0]))).Replace("-", "").ToLowerInvariant(), Is.EqualTo(fields[1]), fields[0]);
            }
            string source = Read(House); string wall = Block(source, 6396298);
            string cells = Section(wall, "m_Tiles", "m_TileAssetArray");
            Assert.That(Regex.Matches(cells, @"- first:").Count, Is.EqualTo(127));
            Assert.That(Regex.Matches(cells, @"m_TileIndex: 3\n").Count, Is.EqualTo(74));
            Assert.That(Regex.Matches(cells, @"m_TileIndex: 0\n").Count, Is.EqualTo(50));
            Assert.That(Regex.Matches(cells, @"m_TileIndex: 1\n").Count, Is.EqualTo(3));
            Assert.That(Regex.Matches(Section(Block(source, 317315563), "m_Tiles", "m_TileAssetArray"), @"- first:").Count, Is.EqualTo(130));
            Assert.That(source, Does.Contain("Rootborn.Game.World.AuthoredWorldScene"));
            Assert.That(Block(source, 1167780941), Does.Contain("_tileSet: {fileID: 11400000, guid: a505ca817cdf2b048b3dda294e030b3a, type: 2}"));
            Assert.That(ReversedHouse(), Is.EqualTo(File.ReadAllBytes(Output + "backup/original/" + House)));
        }

        static T Required<T>(string path) where T : Object { var value = AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(value != null, Is.True, "Missing required House material: " + path); return value; }
        static string Read(string path) { return Encoding.UTF8.GetString(File.ReadAllBytes(path)).Replace("\r\n", "\n"); }
        static string Block(string text, long id) { var m = Regex.Match(text, @"(?ms)^--- !u!\d+ &" + id + @"\r?\n.*?(?=^--- !u!|\z)"); Assert.That(m.Success, Is.True, "Missing block " + id); return m.Value; }
        static string Section(string block, string from, string to) { int a = block.IndexOf("  " + from + ":", StringComparison.Ordinal), b = block.IndexOf("  " + to + ":", StringComparison.Ordinal); Assert.That(a >= 0 && b > a, Is.True); return block.Substring(a, b - a); }
        static string PrefabRef(string text) { var m = Regex.Match(text, @"m_InstancedGameObject: (\{[^}]+\})"); Assert.That(m.Success, Is.True); return m.Groups[1].Value; }
        static string Ref(Object value) { Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id), Is.True); return "{fileID: " + id + ", guid: " + guid + ", type: " + (value is Sprite ? 3 : 2) + "}"; }
        static void Address(string path, string address, string group)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings; Assert.That(settings != null, Is.True);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)); Assert.That(entry != null, Is.True);
            Assert.That(entry.address, Is.EqualTo(address)); Assert.That(entry.parentGroup.Name, Is.EqualTo(group));
            Assert.That(entry.labels, Does.Contain("HouseComfy31"));
            Assert.That(settings.groups.Where(g => g != null).SelectMany(g => g.entries).Count(e => e.address == address), Is.EqualTo(1));
        }
        static byte[] ReversedHouse()
        {
            string source = Encoding.UTF8.GetString(File.ReadAllBytes(House)); string block = Block(source, 6396298); string reversed = block;
            for (int i = 0; i < 2; i++)
            {
                foreach (var pair in new[] { new[] { Tiles[i], OriginalTiles[i] }, new[] { Sprites[i], OriginalSprites[i] } })
                {
                    string guid = AssetDatabase.AssetPathToGUID(pair[0]);
                    if (string.IsNullOrEmpty(guid) || !reversed.Contains(guid)) continue;
                    Assert.That(Regex.Matches(reversed, Regex.Escape(guid)).Count, Is.EqualTo(1));
                    reversed = reversed.Replace(guid, pair[1]);
                }
            }
            return Encoding.UTF8.GetBytes(source.Replace(block, reversed));
        }
    }
}
