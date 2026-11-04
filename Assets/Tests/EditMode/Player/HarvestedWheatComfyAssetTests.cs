using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Player
{
    public sealed class HarvestedWheatComfyAssetTests
    {
        private const string Png = "Assets/Art/UI/UrbanMolding/harvested_wheat.png";
        private const string Item = "Assets/Data/Items/Item_Wheat.asset";
        private const string Address = "sprites/ui/urban-life/harvested-wheat";
        private const string ExpectedSha = "f66c7af4245c6048bae329f0205031d8848bd13f0876085cef14efcc228004b6";
        private const string Preparation = "output/harvested-wheat-20261102-integration/evidence/wheat-preparation.json";

        [Test]
        public void ReviewedIconRetainsImportProfileAndExactNativePixels()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Png);
            Assert.That(sprite, Is.Not.Null, "Approved harvested-wheat icon is not imported.");
            Assert.That(Sha(Png), Is.EqualTo(ExpectedSha));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 64, 64)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(32, 32)));
            var importer = AssetImporter.GetAtPath(Png) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(Png)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(64, 64)));
                Color32[] pixels = raw.GetPixels32();
                Assert.That(pixels.All(c => c.a == 0 || c.a == 255), Is.True);
                Assert.That(pixels.Where(c => c.a == 0).All(c => c.r == 0 && c.g == 0 && c.b == 0), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(c => c.a != 0).Select(ColorKey)).Count, Is.EqualTo(13));
                int[] visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                // Unity decoded rows are bottom-origin; PNG top-origin [4,4,58,60] is symmetric vertically.
                Assert.That(new[] { visible.Min(i => i % 64), visible.Min(i => i / 64), visible.Max(i => i % 64) + 1, visible.Max(i => i / 64) + 1 }, Is.EqualTo(new[] { 4, 4, 58, 60 }));
                for (int y = 0; y < 64; y += 2)
                    for (int x = 0; x < 64; x += 2)
                    {
                        uint value = ColorKey(pixels[y * 64 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 64 + x + 1]), ColorKey(pixels[(y + 1) * 64 + x]), ColorKey(pixels[(y + 1) * 64 + x + 1]) }, Is.All.EqualTo(value));
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void WheatItemUsesDedicatedIconAndExactlyOneSpriteAddress()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Item);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.Icon, Is.Not.Null, "Item_Wheat._icon is the real missing image field.");
            Assert.That(AssetDatabase.GetAssetPath(item.Icon), Is.EqualTo(Png));
            Assert.That(item.Icon, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(Png)));
            Assert.That(AssetDatabase.AssetPathToGUID(Item), Is.EqualTo("64681a5dbbf386240a61ed88c3b3ff95"));
            string guid = AssetDatabase.AssetPathToGUID(Png);
            Assert.That(guid, Does.Match("^[0-9a-f]{32}$"));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entries = settings.groups.Where(g => g != null).SelectMany(g => g.entries).ToArray();
            Assert.That(entries.Count(e => e.guid == guid), Is.EqualTo(1));
            Assert.That(entries.Count(e => e.address == Address), Is.EqualTo(1));
            var entry = settings.FindAssetEntry(guid);
            Assert.That(entry.address, Is.EqualTo(Address));
            Assert.That(AssetDatabase.GetAssetPath(entry.parentGroup), Is.EqualTo("Assets/AddressableAssetsData/AssetGroups/Sprites.asset"));
            Assert.That(entry.labels.Count, Is.EqualTo(0), "Direct ItemDefinition Sprite dependency needs no new theme or preload label.");
            string text = File.ReadAllText(Item);
            Assert.That(Regex.Matches(text, @"(?m)^  _icon: \{fileID: 21300000, guid: " + guid + @", type: 3\}\r?$").Count, Is.EqualTo(1));
        }

        [Test]
        public void ItemFieldsCropAndBothRegistriesRemainByteExactExceptIcon()
        {
            Assert.That(File.Exists(Preparation), Is.True, "Parent must prepare after actual contrast freeze before red execution.");
            string preparationText = File.ReadAllText(Preparation);
            var baseline = JsonUtility.FromJson<PreparationScalars>(preparationText);
            Assert.That(baseline, Is.Not.Null);
            Assert.That(baseline.status, Is.EqualTo("PASS"));
            foreach (var row in ReadPreservationHashes(preparationText))
                Assert.That(PreservedSha(row.Key), Is.EqualTo(row.Value), "Preserved source changed: " + row.Key);
            string backup = baseline.originalItemBackup;
            Assert.That(Sha(backup), Is.EqualTo(baseline.originalItemSha256));
            Assert.That(WithoutIcon(File.ReadAllText(Item)), Is.EqualTo(WithoutIcon(File.ReadAllText(backup))), "Only the single icon reference may differ; all other item bytes stay exact.");
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Item);
            Assert.That(item.Id, Is.EqualTo("Item_Wheat"));
            Assert.That(item.DisplayKey, Is.EqualTo("item.wheat"));
            Assert.That(item.MaxStack, Is.EqualTo(99));
            Assert.That(item.SeedFor, Is.Null);
            string crop = File.ReadAllText("Assets/Data/Crops/Crop_Wheat.asset");
            Assert.That(crop, Does.Contain("_harvestItem: {fileID: 11400000, guid: 64681a5dbbf386240a61ed88c3b3ff95, type: 2}"));
            foreach (string path in new[] { "Assets/Data/Registry/GameDataRegistry.asset", "Assets/Resources/GameDataRegistry.asset" })
            {
                string text = File.ReadAllText(path);
                string items = Regex.Match(text, @"(?ms)^  _items:\r?\n(.*?)(?=^  \w|\z)").Groups[1].Value;
                Assert.That(items, Does.Not.Contain("64681a5dbbf386240a61ed88c3b3ff95"), "Do not silently add pre-existing missing item lookup entries in this icon-only phase.");
                Assert.That(text, Does.Contain("guid: 732817181c9fdb64c97cb0c76976c1d3"));
            }
        }

        [Serializable]
        private sealed class PreparationScalars
        {
            public string status;
            public string originalItemBackup;
            public string originalItemSha256;
        }

        private static Dictionary<string, string> ReadPreservationHashes(string text)
        {
            // The prepared map has ten known ASCII paths and SHA256 scalars. Reject malformed or extra content.
            var blocks = Regex.Matches(text, @"""preservationHashes""\s*:\s*\{(?<body>[^{}]*)\}");
            Assert.That(blocks.Count, Is.EqualTo(1));
            string body = blocks[0].Groups["body"].Value;
            const string pair = @"""(?<path>[A-Za-z0-9_./ -]+)""\s*:\s*""(?<hash>[0-9a-f]{64})""";
            var rows = Regex.Matches(body, pair);
            Assert.That(rows.Count, Is.EqualTo(10));
            Assert.That(string.IsNullOrWhiteSpace(Regex.Replace(body, pair, "").Replace(",", "")), Is.True);
            var expected = new[]
            {
                "Assets/AddressableAssetsData/AssetGroups/Data.asset",
                "Assets/AddressableAssetsData/AssetGroups/Data.asset.meta",
                "Assets/AddressableAssetsData/AssetGroups/Sprites.asset.meta",
                "Assets/Data/Crops/Crop_Wheat.asset",
                "Assets/Data/Crops/Crop_Wheat.asset.meta",
                "Assets/Data/Items/Item_Wheat.asset.meta",
                "Assets/Data/Registry/GameDataRegistry.asset",
                "Assets/Data/Registry/GameDataRegistry.asset.meta",
                "Assets/Resources/GameDataRegistry.asset",
                "Assets/Resources/GameDataRegistry.asset.meta",
            };
            var values = rows.Cast<Match>().ToDictionary(m => m.Groups["path"].Value, m => m.Groups["hash"].Value);
            CollectionAssert.AreEquivalent(expected, values.Keys);
            return values;
        }

        private static string WithoutIcon(string text)
        {
            const string pattern = @"(?m)^  _icon: \{[^\r\n]*\}";
            Assert.That(Regex.Matches(text, pattern).Count, Is.EqualTo(1));
            return Regex.Replace(text, pattern, "  _icon: <single reviewed binding>");
        }
        private static uint ColorKey(Color32 c) => (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);
        // Character appearances are registered later by their own pack. Everything else in a registry stays pinned.
        private static string PreservedSha(string path)
        {
            if (!path.EndsWith("GameDataRegistry.asset", StringComparison.Ordinal)) return Sha(path);
            string text = Regex.Replace(File.ReadAllText(path), @"(?m)^  _characterAppearances:\n(?:  - \{[^\n]*\n)+", "  _characterAppearances: []\n");
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(new System.Text.UTF8Encoding(false).GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static string Sha(string path)
        {
            Assert.That(File.Exists(path), Is.True, "Missing file: " + path);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
