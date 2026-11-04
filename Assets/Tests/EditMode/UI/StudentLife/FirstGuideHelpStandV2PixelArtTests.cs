using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    // Staged v2 text only. Parent imports through the authorized Unity path after reviewing Comfy217.
    // These are saved-asset contracts, not native interaction, prompt-clearance or reload proof.
    public sealed class FirstGuideHelpStandV2PixelArtTests
    {
        private const string StandPath = "Assets/Art/UrbanStreet/Buildings31/first_guide_help_stand.png";
        private const string StandAddress = "sprites/town/comfy31/first-guide-help-stand";
        private const string BookPath = "Assets/Art/SchoolPixels/sprites/book_stack.png";
        private const string BookGuid = "19633f73cbeb1704dbd23748bf52ec9f";
        private const string ActionPath = "Assets/Data/StudentLife/TownHelpActions/TownHelp_FirstGuideErrand.asset";
        private const string ActionGuid = "fbea5e29cdabdf74fb3533af14a045d2";

        [Test]
        public void DedicatedHelpStandImportKeepsReviewedCanvasPivotPointSamplingAndNewAddress()
        {
            var sprite = RequiredAsset<Sprite>(StandPath);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 48, 48)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(24f, 8f)));
            string guid = GuidOf(sprite);
            Assert.That(guid, Is.Not.Empty);
            Assert.That(guid, Is.Not.EqualTo(BookGuid), "The shared book asset must not become the dedicated stand.");
            var importer = AssetImporter.GetAtPath(StandPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(48f));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(.5f, 8f / 48f)));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(48));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            AssertAddress(StandPath, StandAddress);
        }

        [Test]
        public void HelpStandPngKeepsTheAcceptedTwentyFiveColorLowLandscapeTwoPixelSilhouette()
        {
            Assert.That(File.Exists(StandPath), Is.True, "Accepted PNG must be imported before source verification.");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(StandPath)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(48, 48)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                Assert.That(pixels.Where(p => p.a == 0).All(p => p.r == 0 && p.g == 0 && p.b == 0), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.EqualTo(25));
                var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                int left = visible.Min(i => i % 48), right = visible.Max(i => i % 48);
                int bottom = visible.Min(i => i / 48), top = visible.Max(i => i / 48);
                Assert.That(new[] { left, bottom, right + 1, top + 1 }, Is.EqualTo(new[] { 8, 4, 40, 28 }),
                    "Unity bottom-left bounds must match accepted top-left bbox [8,20,40,44].");
                Assert.That(new Vector2Int(right - left + 1, top - bottom + 1), Is.EqualTo(new Vector2Int(32, 24)));
                Assert.That(bottom, Is.EqualTo(4));
                for (int y = 0; y < 48; y += 2)
                    for (int x = 0; x < 48; x += 2)
                    {
                        uint color = ColorKey(pixels[y * 48 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 48 + x + 1]), ColorKey(pixels[(y + 1) * 48 + x]),
                            ColorKey(pixels[(y + 1) * 48 + x + 1]) }, Is.All.EqualTo(color), "Nonuniform2x2 cluster at " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void SavedTownHelpVisualPreservesItsAuthoredGuardPlacementTriggerAndSingleNewBinding()
        {
            // YAML only: no PreviewScene, scene open/load, host mutation or runtime installer call.
            // TownHelpActionRuntimeInstaller.EnsureScene and EnsureHelpPoint both return when
            // AuthoredWorldScene.Find finds this marker. Without it fallback code creates a root
            // marker renderer, repositions prototypes, sets sort50 and changes the trigger to0.85.
            // Marker presence documents that guard assumption; native evidence must verify runtime.
            string scene = SavedTown();
            string parentObject = SavedBlock(scene, 1, 257248086);
            AssertSavedField(parentObject, "m_Name", "AuthoredTownBindings");
            AssertSavedField(parentObject, "m_IsActive", "1");
            Assert.That(SavedComponentIds(parentObject), Is.EqualTo(new[] { "257248087" }));
            string parent = SavedBlock(scene, 4, 257248087);
            AssertIdentityTransform(parent, 257248086, "{x: 0, y: 0, z: 0}", 0);
            Assert.That(SavedChildIds(parent).Count(id => id == "2014801022"), Is.EqualTo(1));
            Assert.That(SavedChildIds(parent).Count(id => id == "2040791385"), Is.EqualTo(1));

            string markerObject = SavedBlock(scene, 1, 2040791384);
            AssertSavedField(markerObject, "m_Name", "AuthoredWorld");
            AssertSavedField(markerObject, "m_IsActive", "1");
            Assert.That(SavedComponentIds(markerObject), Is.EqualTo(new[] { "2040791385", "2040791386" }));
            AssertIdentityTransform(SavedBlock(scene, 4, 2040791385), 2040791384, "{x: 0, y: 0, z: 0}", 257248087);
            string marker = SavedBlock(scene, 114, 2040791386);
            AssertSavedField(marker, "m_GameObject", "{fileID: 2040791384}");
            AssertSavedField(marker, "m_Enabled", "1");
            AssertSavedField(marker, "m_Script", "{fileID: 11500000, guid: 3b9d3d9c8acd9ae4a8d70d5343699639, type: 3}");
            AssertSavedField(marker, "m_EditorClassIdentifier", "Rootborn.Game::Rootborn.Game.World.AuthoredWorldScene");
            Assert.That(Regex.Matches(scene, @"(?m)^  m_EditorClassIdentifier: Rootborn.Game::Rootborn.Game.World.AuthoredWorldScene$").Count, Is.EqualTo(1));

            string rootObject = SavedBlock(scene, 1, 2014801021);
            AssertSavedField(rootObject, "m_Name", "TownHelp_town_help_first_guide_errand");
            AssertSavedField(rootObject, "m_IsActive", "1");
            AssertSavedField(rootObject, "m_Layer", "0");
            Assert.That(SavedComponentIds(rootObject), Is.EqualTo(new[] {
                "2014801022", "2014801026", "2014801025", "2014801024", "2014801023" }),
                "Keep the exact saved root components; no root SpriteRenderer or additional trigger.");
            string root = SavedBlock(scene, 4, 2014801022);
            AssertIdentityTransform(root, 2014801021, "{x: 14, y: 2, z: 0}", 257248087);
            Assert.That(SavedChildIds(root), Is.EqualTo(new[] { "1401220946" }));
            Assert.That(Regex.Matches(scene, @"(?m)^  m_Father: \{fileID: 2014801022\}$").Count, Is.EqualTo(1));
            string visualObject = SavedBlock(scene, 1, 1401220945);
            AssertSavedField(visualObject, "m_Name", "Visual");
            AssertSavedField(visualObject, "m_IsActive", "1");
            Assert.That(SavedComponentIds(visualObject), Is.EqualTo(new[] { "1401220946", "1401220947" }));
            string visual = SavedBlock(scene, 4, 1401220946);
            AssertIdentityTransform(visual, 1401220945, "{x: 0, y: 0.125, z: 0}", 2014801022);
            AssertSavedField(visual, "m_Children", "[]");

            var sprite = RequiredAsset<Sprite>(StandPath);
            string spriteReference = "{fileID: 21300000, guid: " + GuidOf(sprite) + ", type: 3}";
            string renderer = SavedBlock(scene, 212, 1401220947);
            AssertSavedField(renderer, "m_GameObject", "{fileID: 1401220945}");
            AssertSavedField(renderer, "m_Enabled", "1");
            AssertSavedField(renderer, "m_Sprite", spriteReference);
            AssertSavedField(renderer, "m_DrawMode", "0");
            AssertSavedField(renderer, "m_SortingLayerID", "0");
            AssertSavedField(renderer, "m_SortingOrder", "0");
            AssertSavedField(renderer, "m_SpriteSortPoint", "0");
            AssertSavedField(renderer, "m_Color", "{r: 1, g: 1, b: 1, a: 1}");
            AssertSavedField(renderer, "m_FlipX", "0");
            AssertSavedField(renderer, "m_FlipY", "0");
            Assert.That(Regex.Matches(scene, @"(?m)^  m_Sprite: " + Regex.Escape(spriteReference) + "$").Count, Is.EqualTo(1),
                "This dedicated sprite replaces only the existing First Guide Visual.");
            string bookReference = "{fileID: 21300000, guid: " + BookGuid + ", type: 3}";
            Assert.That(Regex.Matches(scene, @"(?m)^  m_Sprite: " + Regex.Escape(bookReference) + "$").Count, Is.EqualTo(2),
                "Only the First Guide use is replaced; the two other saved book consumers remain.");
            float floor = 2f + .125f + (4f - sprite.pivot.y) / sprite.pixelsPerUnit;
            Assert.That(floor, Is.EqualTo(2.0416667f).Within(.000001f));
            Assert.That(floor + 24f / sprite.pixelsPerUnit, Is.EqualTo(2.5416667f).Within(.000001f));
            // Historical prompt bounds predict a gap for this low v2 silhouette; actual rendered clearance still requires native evidence.

            string group = SavedBlock(scene, 210, 2014801025);
            AssertSavedField(group, "m_GameObject", "{fileID: 2014801021}");
            AssertSavedField(group, "m_Enabled", "1");
            AssertSavedField(group, "m_SortingLayerID", "0");
            AssertSavedField(group, "m_SortingOrder", "9904");
            AssertSavedField(group, "m_SortAtRoot", "0");
            string depth = SavedBlock(scene, 114, 2014801024);
            AssertSavedField(depth, "m_GameObject", "{fileID: 2014801021}");
            AssertSavedField(depth, "m_Enabled", "1");
            AssertSavedField(depth, "m_Script", "{fileID: 11500000, guid: 5fad8195c3156224f9205abeae81fea8, type: 3}");
            AssertSavedField(depth, "m_EditorClassIdentifier", "Rootborn.Game::Rootborn.Game.Interiors.GroundDepthSort");
            AssertSavedField(depth, "_footPointOffset", "{x: 0, y: 0}");
            AssertSavedField(depth, "_unitsPerWorld", "48");
            AssertSavedField(depth, "_baseSortingOrder", "10000");
            string trigger = SavedBlock(scene, 61, 2014801026);
            AssertSavedField(trigger, "m_GameObject", "{fileID: 2014801021}");
            AssertSavedField(trigger, "m_Enabled", "1");
            AssertSavedField(trigger, "m_IsTrigger", "1");
            AssertSavedField(trigger, "m_Offset", "{x: 0, y: 0}");
            AssertSavedField(trigger, "m_Size", "{x: 0.7, y: 0.7}");
            string interactor = SavedBlock(scene, 114, 2014801023);
            AssertSavedField(interactor, "m_GameObject", "{fileID: 2014801021}");
            AssertSavedField(interactor, "m_Enabled", "1");
            AssertSavedField(interactor, "m_Script", "{fileID: 11500000, guid: 5b7e96fd3afb8fe4f9c0fddd6d709c7d, type: 3}");
            AssertSavedField(interactor, "m_EditorClassIdentifier", "Rootborn.Game::Rootborn.Game.StudentLife.TownHelpActionInteractor");
            AssertSavedField(interactor, "_action", "{fileID: 11400000, guid: " + ActionGuid + ", type: 2}");
        }

        [Test]
        public void ExistingHelpDefinitionRewardsAndSharedBookRemainUnchanged()
        {
            var action = RequiredAsset<TownHelpActionDefinition>(ActionPath);
            Assert.That(GuidOf(action), Is.EqualTo(ActionGuid));
            Assert.That(action.Id, Is.EqualTo("town.help.first-guide-errand"));
            Assert.That(action.DisplayNameKey, Is.EqualTo("town.help.first-guide-errand"));
            Assert.That(action.NpcId, Is.EqualTo("npc.first-guide"));
            Assert.That(action.LocationId, Is.EqualTo("location.town-square"));
            Assert.That(GuidOf(action.TimePolicy), Is.EqualTo("877e2af40acc7d749a78882afa991e80"));
            Assert.That(action.TimePolicy.TimePointCost, Is.EqualTo(1));
            Assert.That(action.TimePolicy.OncePerDay, Is.False);
            Assert.That(new SerializedObject(action.TimePolicy).FindProperty("_allowedDaysMask").intValue, Is.EqualTo(127));
            Assert.That(action.TimeCostMinutes, Is.EqualTo(20));
            Assert.That(action.EnergyCost, Is.EqualTo(1));
            Assert.That(action.FocusCost, Is.Zero);
            Assert.That(action.StressDelta, Is.Zero);
            Assert.That(action.Requirements, Is.Empty, "The art replacement must not introduce delivery or inventory prerequisites.");
            Assert.That(action.Outcomes.Select(GuidOf), Is.EqualTo(new[] {
                "28767a6f67aec174aaf8fd93c1cb44b0", "eac1c686d4e9f754996cb89eff6adebd" }));
            Assert.That(action.Outcomes[0], Is.TypeOf<HelpActionTraitDeltaOutcome>());
            var trait = (HelpActionTraitDeltaOutcome)action.Outcomes[0];
            Assert.That(GuidOf(trait.Trait), Is.EqualTo("82b98f654b803c3468b3aa21026ae8f0"));
            Assert.That(trait.Trait.Id, Is.EqualTo("trait.responsibility"));
            Assert.That(trait.Delta, Is.EqualTo(2));
            Assert.That(action.Outcomes[1], Is.TypeOf<HelpActionRelationshipDeltaOutcome>());
            var relationship = (HelpActionRelationshipDeltaOutcome)action.Outcomes[1];
            Assert.That(GuidOf(relationship.Relationship), Is.EqualTo("3fe25332108980742b1b38dfa7f31086"));
            Assert.That(relationship.Relationship.Id, Is.EqualTo("relationship.first-guide.trust"));
            Assert.That(relationship.Relationship.TargetId, Is.EqualTo("npc.first-guide"));
            Assert.That(relationship.Delta, Is.EqualTo(2));
            var registry = RequiredAsset<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry.TownHelpActions.Count(value => value == action), Is.EqualTo(1));

            var book = RequiredAsset<Sprite>(BookPath);
            Assert.That(GuidOf(book), Is.EqualTo(BookGuid));
            Assert.That(book.rect, Is.EqualTo(new Rect(0, 0, 24, 18)));
            Assert.That(book.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(book.pivot, Is.EqualTo(new Vector2(12f, 6f)));
            AssertAddress(BookPath, "sprites/school/sprites/book-stack");
            Assert.That(Sha256(BookPath), Is.EqualTo("7abb9567e44d8f881609908196778d4c0bc715224ad3054aa897d01a08d34b9f"));
            Assert.That(Sha256(BookPath + ".meta"), Is.EqualTo("ba8068f9521c15349bac54c3901bb174fb63575c58d5013bdb7bd2370a2d2eae"));
            string scene = SavedTown();
            string bookReference = "{fileID: 21300000, guid: " + BookGuid + ", type: 3}";
            AssertSavedField(SavedBlock(scene, 212, 1346946719), "m_Sprite", bookReference);
            AssertSavedField(SavedBlock(scene, 212, 1645727378), "m_Sprite", bookReference);
            // This test preserves SO configuration; it does not execute help or claim reload behavior.
            // The in-memory request counter's possible persisted-ID collision remains a separate repro task.
        }

        private static string SavedTown() => File.ReadAllText("Assets/Scenes/Town.unity").Replace("\r\n", "\n");

        private static string SavedBlock(string scene, int typeId, long fileId)
        {
            var matches = Regex.Matches(scene, @"(?ms)^--- !u!" + typeId + " &" + fileId + @"\n.*?(?=^---|\z)");
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one saved block of type " + typeId + " and fileID " + fileId);
            return matches[0].Value;
        }

        private static string[] SavedComponentIds(string block) => Regex.Matches(block, @"(?m)^  - component: \{fileID: (\d+)\}$")
            .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();

        private static string[] SavedChildIds(string block) => Regex.Matches(block, @"(?m)^  - \{fileID: (\d+)\}$")
            .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();

        private static void AssertIdentityTransform(string block, long objectId, string position, long parentId)
        {
            AssertSavedField(block, "m_GameObject", "{fileID: " + objectId + "}");
            AssertSavedField(block, "m_LocalPosition", position);
            AssertSavedField(block, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            AssertSavedField(block, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            AssertSavedField(block, "m_Father", "{fileID: " + parentId + "}");
        }

        private static void AssertSavedField(string block, string field, string expected)
        {
            var matches = Regex.Matches(block, @"(?m)^  " + Regex.Escape(field) + @": ?([^\n]*)$");
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one serialized field " + field);
            Assert.That(matches[0].Groups[1].Value, Is.EqualTo(expected), "Saved field " + field);
        }

        private static T RequiredAsset<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null, "Required authored asset missing: " + path);
            return value;
        }

        private static string GuidOf(Object value) => value == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));

        private static void AssertAddress(string path, string expected)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(expected));
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
