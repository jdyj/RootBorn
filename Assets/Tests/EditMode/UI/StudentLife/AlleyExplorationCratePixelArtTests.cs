using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class AlleyExplorationCratePixelArtTests
    {
        private const string CratePath = "Assets/Art/UrbanStreet/Buildings31/alley_exploration_crate.png";
        private const string CrateAddress = "sprites/town/comfy31/alley-exploration-crate";
        private const string BookPath = "Assets/Art/SchoolPixels/sprites/book_open.png";
        private const string InteractionPath = "Assets/Data/StudentLife/ExplorationInteractions/Exploration_TestAlleyCrate.asset";
        private const string RiskGuid = "5a7700890de97d14d9050d84ef15a6c9";

        [Test]
        public void DedicatedCrateImportKeepsReviewedGeometryAndLeavesTheOriginalBookRegistered()
        {
            var sprite = RequiredAsset<Sprite>(CratePath);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 48, 48)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(24f, 11f)));
            var importer = AssetImporter.GetAtPath(CratePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            AssertAddress(CratePath, CrateAddress);
            Assert.That(AssetDatabase.AssetPathToGUID(CratePath), Is.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(CratePath), Is.Not.EqualTo("2f47b5b9577c3e34daf0662dcdb20fb1"));
            var book = RequiredAsset<Sprite>(BookPath);
            Assert.That(AssetDatabase.AssetPathToGUID(BookPath), Is.EqualTo("2f47b5b9577c3e34daf0662dcdb20fb1"));
            Assert.That(book.rect, Is.EqualTo(new Rect(0, 0, 24, 14)));
            Assert.That(book.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(book.pivot, Is.EqualTo(new Vector2(12f, 7f)));
            AssertAddress(BookPath, "sprites/school/sprites/book-open");
        }

        [Test]
        public void CrateSourceUsesBinaryTwoPixelArtWithTheReviewedSilhouetteAndFloorMargin()
        {
            Assert.That(File.Exists(CratePath), Is.True, "Reviewed crate PNG must exist before source verification.");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(CratePath)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(48, 48)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.InRange(1, 31));
                var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                int left = visible.Min(i => i % 48), right = visible.Max(i => i % 48);
                int bottom = visible.Min(i => i / 48), top = visible.Max(i => i / 48);
                Assert.That(left, Is.GreaterThanOrEqualTo(4));
                Assert.That(right, Is.LessThanOrEqualTo(43));
                Assert.That(right - left + 1, Is.LessThanOrEqualTo(40));
                Assert.That(top - bottom + 1, Is.LessThanOrEqualTo(36));
                Assert.That(bottom, Is.EqualTo(4), "The visible feet must retain four transparent rows below them.");
                for (int y = 0; y < 48; y += 2)
                    for (int x = 0; x < 48; x += 2)
                    {
                        uint color = ColorKey(pixels[y * 48 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 48 + x + 1]), ColorKey(pixels[(y + 1) * 48 + x]),
                            ColorKey(pixels[(y + 1) * 48 + x + 1]) }, Is.All.EqualTo(color), "Nonuniform 2x2 cluster at " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void SavedAlleyPointReplacesOnlyItsRootVisualAndPreservesTheAuthoredInteractionGeometry()
        {
            // Read saved source without instantiating Town's URP lights into another fixture's host.
            // Actual runtime renderer, depth and interaction behavior are covered by native input evidence.
            string scene = File.ReadAllText("Assets/Scenes/Town.unity").Replace("\r\n", "\n");
            string parentObject = SavedBlock(scene, 1, 257248086);
            AssertSavedField(parentObject, "m_Name", "AuthoredTownBindings");
            AssertSavedField(parentObject, "m_IsActive", "1");
            Assert.That(SavedComponentIds(parentObject), Is.EqualTo(new[] { "257248087" }), "No ancestor SortingGroup is added.");
            string parent = SavedBlock(scene, 4, 257248087);
            AssertSavedField(parent, "m_GameObject", "{fileID: 257248086}");
            AssertSavedField(parent, "m_LocalPosition", "{x: 0, y: 0, z: 0}");
            AssertSavedField(parent, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            AssertSavedField(parent, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            AssertSavedField(parent, "m_Father", "{fileID: 0}");
            Assert.That(Regex.Matches(parent, @"(?m)^  - \{fileID: 519262772\}$").Count, Is.EqualTo(1));

            string rootObject = SavedBlock(scene, 1, 519262771);
            AssertSavedField(rootObject, "m_Name", "TownExplorationInteractionPoint");
            AssertSavedField(rootObject, "m_IsActive", "1");
            AssertSavedField(rootObject, "m_Layer", "0");
            Assert.That(SavedComponentIds(rootObject), Is.EqualTo(new[] {
                "519262772", "519262777", "519262776", "519262775", "519262774", "519262773" }),
                "Retain exactly the transform, one trigger, one sorting group, depth sort, renderer and interactor.");
            string root = SavedBlock(scene, 4, 519262772);
            AssertSavedField(root, "m_GameObject", "{fileID: 519262771}");
            AssertSavedField(root, "m_LocalPosition", "{x: 22, y: 2, z: 0}");
            AssertSavedField(root, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            AssertSavedField(root, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            AssertSavedField(root, "m_Children", "[]");
            AssertSavedField(root, "m_Father", "{fileID: 257248087}");
            Assert.That(Regex.Matches(scene, @"(?m)^  m_Father: \{fileID: 519262772\}$").Count, Is.Zero,
                "The point keeps its root renderer with no Visual child or added child collider.");

            var sprite = RequiredAsset<Sprite>(CratePath);
            string spriteReference = "{fileID: 21300000, guid: " + GuidOf(sprite) + ", type: 3}";
            string renderer = SavedBlock(scene, 212, 519262774);
            AssertSavedField(renderer, "m_GameObject", "{fileID: 519262771}");
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
                "The dedicated crate is used at this existing saved point only.");
            Assert.That(2f + (4f - sprite.pivot.y) / sprite.pixelsPerUnit, Is.EqualTo(2f - 7f / 48f).Within(0.000001f),
                "Saved identity parent and root Y2 preserve visible floor1.8541667.");

            string group = SavedBlock(scene, 210, 519262776);
            AssertSavedField(group, "m_GameObject", "{fileID: 519262771}");
            AssertSavedField(group, "m_Enabled", "1");
            AssertSavedField(group, "m_SortingLayerID", "0");
            AssertSavedField(group, "m_SortingOrder", "9904");
            AssertSavedField(group, "m_SortAtRoot", "0");
            string depth = SavedBlock(scene, 114, 519262775);
            AssertSavedField(depth, "m_GameObject", "{fileID: 519262771}");
            AssertSavedField(depth, "m_Enabled", "1");
            AssertSavedField(depth, "m_Script", "{fileID: 11500000, guid: 5fad8195c3156224f9205abeae81fea8, type: 3}");
            AssertSavedField(depth, "m_EditorClassIdentifier", "Rootborn.Game::Rootborn.Game.Interiors.GroundDepthSort");
            AssertSavedField(depth, "_footPointOffset", "{x: 0, y: 0}");
            AssertSavedField(depth, "_unitsPerWorld", "48");
            AssertSavedField(depth, "_baseSortingOrder", "10000");
            string trigger = SavedBlock(scene, 61, 519262777);
            AssertSavedField(trigger, "m_GameObject", "{fileID: 519262771}");
            AssertSavedField(trigger, "m_Enabled", "1");
            AssertSavedField(trigger, "m_IsTrigger", "1");
            AssertSavedField(trigger, "m_Offset", "{x: 0, y: 0}");
            AssertSavedField(trigger, "m_Size", "{x: 0.7, y: 0.7}"); // Saved trigger; runtime SO radius remains1.
            string interactor = SavedBlock(scene, 114, 519262773);
            AssertSavedField(interactor, "m_GameObject", "{fileID: 519262771}");
            AssertSavedField(interactor, "m_Enabled", "1");
            AssertSavedField(interactor, "m_Script", "{fileID: 11500000, guid: f3baf9392ae5db441a166e0654ad5e8a, type: 3}");
            AssertSavedField(interactor, "m_EditorClassIdentifier", "Rootborn.UI::Rootborn.UI.StudentLife.ExplorationInteractionPointInteractor");
            AssertSavedField(interactor, "_interaction", "{fileID: 11400000, guid: "
                + GuidOf(RequiredAsset<ExplorationInteractionDefinition>(InteractionPath)) + ", type: 2}");
        }

        [Test]
        public void ExistingExplorationDefinitionRetainsItsFourOrderedChoicesRiskAndOutcomeReferences()
        {
            var interaction = RequiredAsset<ExplorationInteractionDefinition>(InteractionPath);
            Assert.That(GuidOf(interaction), Is.EqualTo("cfd09340758f7bf4080cdd77b51faa94"));
            Assert.That(interaction.Id, Is.EqualTo("exploration.test.alley-crate"));
            Assert.That(interaction.DisplayNameKey, Is.EqualTo("Exploration Test Alley Crate"));
            Assert.That(interaction.DescriptionKey, Is.EqualTo("Exploration Test Alley Crate"));
            Assert.That(interaction.WorldPosition, Is.EqualTo(new Vector2(22f, 2f)));
            Assert.That(interaction.InteractionRadius, Is.EqualTo(1f));
            Assert.That(interaction.Repeatable, Is.False);
            Assert.That(interaction.CooldownDays, Is.Zero);
            Assert.That(GuidOf(interaction.DefaultRiskPolicy), Is.EqualTo(RiskGuid));
            var risk = new SerializedObject(interaction.DefaultRiskPolicy);
            Assert.That(risk.FindProperty("_stressDelta").intValue, Is.EqualTo(1));
            Assert.That(risk.FindProperty("_energyCost").intValue, Is.EqualTo(1));
            Assert.That(risk.FindProperty("_focusCost").intValue, Is.Zero);
            Assert.That(risk.FindProperty("_status").objectReferenceValue, Is.Null);
            Assert.That(risk.FindProperty("_statusDelta").intValue, Is.Zero);
            var registry = RequiredAsset<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry.ExplorationInteractions.Count(value => value == interaction), Is.EqualTo(1));
            string[] ids = { "inspect", "careful", "discovery", "leave" };
            string[] choices = { "f9f8318e1119a124f9538cbe2f21af8f", "18d505be67de8a34c866d1c9e3e9c546", "6c00eff74fb5a13488b03a62f14bfda2", "2650a04f8aebf9c4f8f865947e8b6ac6" };
            string[][] conditions = { new string[0], new[] { "502e9c9028a017d4d8b3b9b6f11c6f43" }, new string[0], new string[0] };
            string[][] outcomes = {
                new[] { "9a03fe75eb7269d43bd0f91b8cdead44", "2cc83fa0a49c6484b99756210e0ae0b0" },
                new[] { "2ba9ae1f4dfeed248bede276fc60b447", "2cc83fa0a49c6484b99756210e0ae0b0" },
                new[] { "1d5464b8e716336419f13a43b3a39e8a", "b7f46002b0b4ed343a7c3cd32a62081b", "d143f7b070f84534995ea28d924566a9" },
                new string[0]
            };
            Assert.That(interaction.Choices.Count, Is.EqualTo(4));
            for (int i = 0; i < ids.Length; i++)
            {
                var choice = interaction.Choices[i];
                Assert.That(GuidOf(choice), Is.EqualTo(choices[i]));
                Assert.That(choice.Id, Is.EqualTo("choice.test." + ids[i]));
                Assert.That(choice.DisplayNameKey, Is.EqualTo(choice.Id));
                Assert.That(choice.DescriptionKey, Is.EqualTo(choice.Id + ".desc"));
                Assert.That(choice.PreviewHintKey, Is.EqualTo(choice.Id + ".preview"));
                Assert.That(choice.Conditions.Select(GuidOf), Is.EqualTo(conditions[i]));
                Assert.That(choice.Outcomes.Select(GuidOf), Is.EqualTo(outcomes[i]));
                Assert.That(GuidOf(choice.RiskPolicy), Is.EqualTo(i < 3 ? RiskGuid : string.Empty));
                Assert.That(choice.Repeatable, Is.EqualTo(i == 3));
            }
            // Exact PNG/meta and complete downstream SO byte preservation is recorded by the authoring before/after proof.
        }

        private static string SavedBlock(string scene, int typeId, long fileId)
        {
            var matches = Regex.Matches(scene, @"(?ms)^--- !u!" + typeId + " &" + fileId + @"\n.*?(?=^---|\z)");
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one saved block of type " + typeId + " and fileID " + fileId);
            return matches[0].Value;
        }

        private static string[] SavedComponentIds(string block) => Regex.Matches(block, @"(?m)^  - component: \{fileID: (\d+)\}$")
            .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();

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

        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
