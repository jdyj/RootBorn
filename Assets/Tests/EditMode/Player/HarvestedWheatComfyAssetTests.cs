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
            if (path.EndsWith("AssetGroups/Data.asset", StringComparison.Ordinal)) return ShaOfText(WithoutReviewedDreamJobEntries(File.ReadAllText(path)));
            if (!path.EndsWith("GameDataRegistry.asset", StringComparison.Ordinal)) return Sha(path);
            string text = Regex.Replace(File.ReadAllText(path), @"(?m)^  _characterAppearances:\n(?:  - \{[^\n]*\n)+", "  _characterAppearances: []\n");
            text = WithoutReviewedDreamJobDelta(text, path.StartsWith("Assets/Data/", StringComparison.Ordinal));
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(new System.Text.UTF8Encoding(false).GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        // Dream and part-time job registration is the one reviewed registry change after this pin (2026-11-04).
        // Undo exactly that change. Any other registry edit still changes the hash.
        private static readonly Dictionary<string, string[]> ReviewedDreamJobRegistryGuids = new Dictionary<string, string[]>
        {
            { "_questConditions", new[] { "1d3d1aa87c7af93448106b347b67b6f5", "e29c6515e91562f4ab125cb7285b74c9" } },
            { "_careerHints", new[] { "a50f11080a395dd5beec84fffd07ba46", "83a89e3296b35da485ab24083efe9c47", "d6ca2f13fa725fea81a908e125b37b44", "bae0ed0476ab53e18b66a3e1793e369b", "003872287d0454998c9587c5024207ca", "184fbe59882852708c2328d6eebf18b8", "0344bb6d6b7651a79fe4ba061eddad0a", "0c05384b668151688a0eeacd2eae65fa", "091cda194ef754b8952ea5e7a36a468a", "f0d17e4363105a478b7e953c3ddf2725", "2b968b9cd1a05302b794220638e961a1", "51bcd0836feb5e7594f983e61051b63a" } },
            { "_careerInterests", new[] { "dc7be1b9c80f5bef8f0a6b93428875b3", "f01d09f8f88451f9904b43a8c332aaf0", "e8e0dcc6402654c4bcb63f40a524d0d2", "52b0edd65acf568898a9d25043a92677" } },
            { "_workplaces", new[] { "4c7c31fc9de853908e0a53177932a7a9", "7cadbd0846465d9d952762bf8412aadb", "8355c562fe035ec689ba2f3ef0ca1dad", "ee5cc81f7dc05fdc8cd08509109d9362" } },
            { "_partTimeWorks", new[] { "c9c289becea45b908d28c435fb6f0b59", "9a84060315765314b1a346d162844e4f", "81e2783369e45e96aa6454ed51b4c620", "111a8911ca6f55afa8e339d56a8d2d8c", "06601f4b59185f48b13468c7af3ed463", "f047c2d0fd7e553d83a2994c2747911b", "84aee1fb502b5814bf8492ba6105b0bb" } },
            { "_campaignRoutes", new[] { "a3550c42bb165edfbdb10a1a8b13bde1", "5483e5fa525b5d839d09efdb5e42a627", "1bf8a79f901d50d383cca2d699660a84", "4569816ae80c589f8fee265269360b25", "8e60ebb2c462501bb184d3a8afdea0f0", "d6b5102df7945942b49817398b8a4e50", "04c0e372355d5944959a271a3257f4c5" } },
            { "_campaignObjectives", new[] { "a2ec62f421fa5ca69243e4095d4f16b2", "3bb20531016b510aa13b7aa69d27dd3f", "0ab133c88db75a46a3a06f5c5eb84db4", "4446a52519675e3b8762ea573c941405", "dffdbf719ea35e38a656102dcaad8e26", "55b601517ea45a5fa802c37fcecc92a6", "f88c7786a22a58289f39e23ab301c149" } },
            { "_questObjectives", new[] { "527826c77c5456adbe0dd360d45d9b4e" } }
        };
        private static readonly string[] RemovedInterestGuids = { "01fc1513fb75ac448aaa6b5d5c9a7ac3", "b2e63186a49d1594f86424c4b1278a8c", "c1348a30d88a23c429ab668057894200", "6e2710dfaee3eff4c903a56f0ae33656" };

        private static string WithoutReviewedDreamJobDelta(string text, bool primaryRegistry)
        {
            foreach (var field in ReviewedDreamJobRegistryGuids)
            {
                foreach (string guid in field.Value)
                {
                    string line = "  - {fileID: 11400000, guid: " + guid + ", type: 2}\n";
                    Assert.That(Regex.Matches(text, Regex.Escape(line)).Count, Is.EqualTo(1), "Reviewed registration must appear exactly once: " + guid);
                    var block = Regex.Match(text, @"(?m)^  " + field.Key + @":\n(?:  - \{[^\n]*\n)*");
                    Assert.That(block.Value, Does.Contain(line), guid + " must stay in " + field.Key);
                    text = text.Remove(block.Index, block.Length).Insert(block.Index, block.Value.Replace(line, string.Empty));
                }
            }
            string removed = string.Concat(RemovedInterestGuids.Select(guid => "  - {fileID: 11400000, guid: " + guid + ", type: 2}\n"));
            text = text.Replace("  _careerInterests:\n", primaryRegistry ? "  _careerInterests:\n" + removed : "  _careerInterests: []\n");
            // Both registries had no quest conditions before the story pacing conditions.
            text = text.Replace("  _questConditions:\n", "  _questConditions: []\n");
            // The fallback registry had these lists empty.
            if (!primaryRegistry)
                foreach (string field in new[] { "_careerHints", "_workplaces", "_partTimeWorks" })
                    text = text.Replace("  " + field + ":\n", "  " + field + ": []\n");
            return text;
        }

        // Addressable entries of the reviewed dream and job assets. Each must appear once; any other group change still changes the hash.
        private static readonly string[] ReviewedDreamJobAddressableGuids =
        {
            "1d3d1aa87c7af93448106b347b67b6f5",
            "e29c6515e91562f4ab125cb7285b74c9",
            "003872287d0454998c9587c5024207ca",
            "0344bb6d6b7651a79fe4ba061eddad0a",
            "04c0e372355d5944959a271a3257f4c5",
            "06601f4b59185f48b13468c7af3ed463",
            "091cda194ef754b8952ea5e7a36a468a",
            "0ab133c88db75a46a3a06f5c5eb84db4",
            "0c05384b668151688a0eeacd2eae65fa",
            "0c78cd3d74c9535cbb7f786359db7fad",
            "111a8911ca6f55afa8e339d56a8d2d8c",
            "11e24882f9b35dc79076f2cde3bbf498",
            "183ab5f4ae3c5533a839b0d33646e2d0",
            "184fbe59882852708c2328d6eebf18b8",
            "196f13689f345812a84de534e58ba176",
            "1bf8a79f901d50d383cca2d699660a84",
            "2b968b9cd1a05302b794220638e961a1",
            "324c9bc5b9b65b81be62133c760a392b",
            "39482081af195e98b7f8a6c710d9acfa",
            "3bb20531016b510aa13b7aa69d27dd3f",
            "4446a52519675e3b8762ea573c941405",
            "4569816ae80c589f8fee265269360b25",
            "4824862897275753bd249a052c9ca0e6",
            "4c7c31fc9de853908e0a53177932a7a9",
            "4f0ed990500c58f597e53349d52b3391",
            "5158cbe1304c52f788458ff19db1535c",
            "51bcd0836feb5e7594f983e61051b63a",
            "527826c77c5456adbe0dd360d45d9b4e",
            "52b0edd65acf568898a9d25043a92677",
            "5300193a39815c699bc6f2de364b8a6e",
            "5483e5fa525b5d839d09efdb5e42a627",
            "55b601517ea45a5fa802c37fcecc92a6",
            "57c34e3296745bd7b5aa7a2e71130702",
            "6449ed839385522b825eb83b05908a67",
            "6c52088483245be39e4a523707abf29b",
            "6d184cdb98be572b869822ed5a5e8f2a",
            "7248c195e0f15da788001ebae5441e53",
            "775beeeaa2e8575f8bc6abf75c425bc6",
            "7b4dd7c174135fba8125215bca1c1d2f",
            "7cadbd0846465d9d952762bf8412aadb",
            "81e2783369e45e96aa6454ed51b4c620",
            "8355c562fe035ec689ba2f3ef0ca1dad",
            "83a89e3296b35da485ab24083efe9c47",
            "8466408e8aac57c28a6681bc8323339c",
            "84aee1fb502b5814bf8492ba6105b0bb",
            "8e60ebb2c462501bb184d3a8afdea0f0",
            "8f8cce2c595a58e392e7a91182adc631",
            "954c8add39ab5239a72274cae6bded83",
            "9711ad7e9ee85eacaa2d979e0fe251f4",
            "9a84060315765314b1a346d162844e4f",
            "9d1337835007518cacefb7128f9055f9",
            "a21d5827a5115bc393ef5086f2b91e6d",
            "a2ec62f421fa5ca69243e4095d4f16b2",
            "a3550c42bb165edfbdb10a1a8b13bde1",
            "a50f11080a395dd5beec84fffd07ba46",
            "a9991b686e11579691e9f9a00d0f7518",
            "b1ce042c5a9c55339f71712f659cc6ef",
            "bae0ed0476ab53e18b66a3e1793e369b",
            "bcb5e81c6bf45e628fafa25203c9b3a1",
            "bddc546c93d05706b2b131e5d85acaf9",
            "c7b612b47553516baf4d9f4c6f58f837",
            "c9c289becea45b908d28c435fb6f0b59",
            "d2dee5793e775958bc62c72b68eff50a",
            "d6b5102df7945942b49817398b8a4e50",
            "d6ca2f13fa725fea81a908e125b37b44",
            "d709ddd1ff8d5de692e2e57d1ca35393",
            "d7437f07738458069524aeb2281b8829",
            "db2233c940a158ca910a73f521fa8ca0",
            "dc7be1b9c80f5bef8f0a6b93428875b3",
            "dffdbf719ea35e38a656102dcaad8e26",
            "e073a8113148597eb7a0be2d8c0710a7",
            "e7adac1964fd52449487522421cff64d",
            "e8e0dcc6402654c4bcb63f40a524d0d2",
            "ee5cc81f7dc05fdc8cd08509109d9362",
            "f01d09f8f88451f9904b43a8c332aaf0",
            "f047c2d0fd7e553d83a2994c2747911b",
            "f0d17e4363105a478b7e953c3ddf2725",
            "f32e415df46b5e23a04db51b67c4e70a",
            "f88c7786a22a58289f39e23ab301c149"
        };

        private static string WithoutReviewedDreamJobEntries(string text)
        {
            foreach (string guid in ReviewedDreamJobAddressableGuids)
            {
                var entries = Regex.Matches(text, @"  - m_GUID: " + guid + @"\n(?:    [^\n]*\n)+");
                Assert.That(entries.Count, Is.EqualTo(1), "Reviewed Addressables entry must appear exactly once: " + guid);
                text = text.Remove(entries[0].Index, entries[0].Length);
            }
            return text;
        }

        private static string ShaOfText(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(new System.Text.UTF8Encoding(false).GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static string Sha(string path)
        {
            Assert.That(File.Exists(path), Is.True, "Missing file: " + path);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
