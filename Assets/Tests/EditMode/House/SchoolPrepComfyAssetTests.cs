using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.House
{
    public sealed class SchoolPrepComfyAssetTests
    {
        private const string ArtRoot = "Assets/Art/HousePixels/SchoolPrep31/";
        private const string ScenePath = "Assets/Scenes/House.unity";

        [TestCase("school_prep_notebook", 32, 24, 16, 2, "school-prep-notebook")]
        [TestCase("school_prep_name_tag", 24, 16, 12, 2, "school-prep-name-tag")]
        public void DedicatedPropHasCrispSingleSpriteImportAndOwnAddress(string name, int width, int height, int pivotX, int pivotY, string address)
        {
            string path = ArtRoot + name + ".png";
            var sprite = RequiredSprite(path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, width, height)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(pivotX, pivotY)));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(addressables, Is.Not.Null);
            var entry = addressables.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, "Dedicated prop must have its own Addressables entry.");
            Assert.That(entry.address, Is.EqualTo("sprites/house/comfy31/" + address));
        }

        [TestCase("school_prep_notebook", 32, 24, 2, 2, 30, 22, "da5a3993e80737d585856cd296b93de3ce2dd4a1b3126ec3cd093b2cead1d6cc")]
        [TestCase("school_prep_name_tag", 24, 16, 2, 2, 20, 14, "8fd680b3d8e18ab72bb571b0d3841c3ea6efdbf70056171ec48ce0f7deb1d40e")]
        public void DedicatedPropRetainsReviewedPixelsBinaryAlphaAndTwoPixelClusters(string name, int width, int height, int left, int bottom, int right, int top, string acceptedSha)
        {
            string path = ArtRoot + name + ".png";
            RequiredSprite(path);
            Assert.That(Sha(path), Is.EqualTo(acceptedSha), "Import must retain the selected reviewed design pixels.");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(width, height)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                Assert.That(pixels.Where(p => p.a == 0).All(p => p.r == 0 && p.g == 0 && p.b == 0), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.InRange(1, 31));
                var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                Assert.That(new[] { visible.Min(i => i % width), visible.Min(i => i / width), visible.Max(i => i % width) + 1, visible.Max(i => i / width) + 1 }, Is.EqualTo(new[] { left, bottom, right, top }));
                for (int y = 0; y < height; y += 2)
                    for (int x = 0; x < width; x += 2)
                    {
                        uint expected = ColorKey(pixels[y * width + x]);
                        Assert.That(new[] { ColorKey(pixels[y * width + x + 1]), ColorKey(pixels[(y + 1) * width + x]), ColorKey(pixels[(y + 1) * width + x + 1]) }, Is.All.EqualTo(expected), "Nonuniform2x2 cluster at " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void SavedHouseNotebookUsesDedicatedRustSpriteAtCompensatedScaleAndSameContact()
        {
            var sprite = RequiredSprite(ArtRoot + "school_prep_notebook.png");
            string scene = ReadScene();
            AssertVisual(scene, 2073228312, 2073228313, 2073228314, "Notebook", "{x: 0, y: 0.9, z: 0}", "{x: 1, y: 1, z: 1}", sprite);
            var bounds = AlphaBounds(ArtRoot + "school_prep_notebook.png");
            var oldBounds = AlphaBounds("Assets/Art/CareerFacilities/Supplement/notebook.png");
            Assert.That(-2.5f + .9f + (bounds.yMin - sprite.pivot.y) / sprite.pixelsPerUnit, Is.EqualTo(-1.6f).Within(.000001f));
            Assert.That(bounds.width / sprite.pixelsPerUnit, Is.EqualTo(oldBounds.width * 2f / 48f).Within(.000001f), "New visible width preserves old scaled notebook footprint.");
            Assert.That(bounds.height / sprite.pixelsPerUnit, Is.EqualTo(oldBounds.height * 2f / 48f).Within(.000001f));
        }

        [Test]
        public void SavedHouseNameTagUsesDedicatedSpriteWithoutMovingOrRescalingItsContact()
        {
            var sprite = RequiredSprite(ArtRoot + "school_prep_name_tag.png");
            AssertVisual(ReadScene(), 2055273352, 2055273353, 2055273354, "NameTag", "{x: 0.6, y: 0.95, z: 0}", "{x: 1.5, y: 1.5, z: 1.5}", sprite);
            const string oldPath = "Assets/Art/CareerFacilities/Supplement/child_name_tag.png";
            var oldSprite = RequiredSprite(oldPath);
            float oldContact = -2.5f + .95f + (AlphaBounds(oldPath).yMin - oldSprite.pivot.y) * 1.5f / oldSprite.pixelsPerUnit;
            float newContact = -2.5f + .95f + (AlphaBounds(ArtRoot + "school_prep_name_tag.png").yMin - sprite.pivot.y) * 1.5f / sprite.pixelsPerUnit;
            Assert.That(newContact, Is.EqualTo(oldContact).Within(.000001f));
        }

        [Test]
        public void DailyPlanBoardRetainsRootSortingInteractionTriggerAndSolidDesk()
        {
            // Saved YAML inspection avoids opening scenes or triggering unrelated live services.
            string scene = ReadScene();
            Field(Block(scene, 1, 109339617), "m_Name", "DailyPlanBoard");
            Assert.That(Components(Block(scene, 1, 109339617)), Is.EqualTo(new[] { "109339618", "109339622", "109339621", "109339620", "109339619" }));
            string root = Block(scene, 4, 109339618);
            Field(root, "m_LocalPosition", "{x: 3.5, y: -2.5, z: 0}");
            Field(root, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            Field(root, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            Field(root, "m_Father", "{fileID: 1027094402}");
            string ownRoom = Block(scene, 4, 1027094402);
            Field(ownRoom, "m_LocalPosition", "{x: 0, y: 0, z: 0}");
            Field(ownRoom, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            Field(ownRoom, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            Field(Block(scene, 210, 109339622), "m_SortingOrder", "10120");
            Field(Block(scene, 114, 109339621), "_footPointOffset", "{x: 0, y: 0}");
            Field(Block(scene, 114, 109339619), "m_EditorClassIdentifier", "Rootborn.UI::Rootborn.UI.StudentLife.DailyPlanBoardInteractor");
            Field(Block(scene, 114, 109339619), "_routeSets", "[]");
            string trigger = Block(scene, 61, 109339620);
            Field(trigger, "m_Enabled", "1"); Field(trigger, "m_IsTrigger", "1");
            Field(trigger, "m_Offset", "{x: 0, y: 0}"); Field(trigger, "m_Size", "{x: 0.8, y: 0.65}");
            string desk = Block(scene, 4, 1059658780);
            Field(desk, "m_LocalPosition", "{x: 0, y: 0, z: 0}");
            Field(desk, "m_LocalScale", "{x: 1, y: 1, z: 1}");
            Field(desk, "m_Father", "{fileID: 109339618}");
            string collider = Block(scene, 61, 1059658781);
            Field(collider, "m_Enabled", "1"); Field(collider, "m_IsTrigger", "0");
            Field(collider, "m_Offset", "{x: 0, y: 0.55}"); Field(collider, "m_Size", "{x: 1.65, y: 0.7}");
        }

        [Test]
        public void SharedOriginalNotebookAndChildNameTagPngsRemainByteExact()
        {
            const string root = "Assets/Art/CareerFacilities/Supplement/";
            Assert.That(Sha(root + "notebook.png"), Is.EqualTo("ba31bb5216c9ef095b8f3772f4f94e293b47c4bdbc547adac09d0b3b6b17b83a"));
            Assert.That(Sha(root + "child_name_tag.png"), Is.EqualTo("1bf07f7f5ea47db1d166d300ac8104bfd9cb670693c14ef83331689387360cc0"));
            Assert.That(AssetDatabase.AssetPathToGUID(root + "notebook.png"), Is.EqualTo("513cbdb1dfaec444ca68d1a855dcb78b"));
            Assert.That(AssetDatabase.AssetPathToGUID(root + "child_name_tag.png"), Is.EqualTo("cdf6ac1ff667e7e40acf07c1d694600c"));
        }

        private static void AssertVisual(string scene, long go, long transform, long renderer, string name, string position, string scale, Sprite sprite)
        {
            string obj = Block(scene, 1, go);
            Field(obj, "m_Name", name); Field(obj, "m_IsActive", "1");
            Assert.That(Components(obj), Is.EqualTo(new[] { transform.ToString(), renderer.ToString() }), "Decorative child gains no gameplay component.");
            string pose = Block(scene, 4, transform);
            Field(pose, "m_LocalPosition", position); Field(pose, "m_LocalScale", scale);
            Field(pose, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}");
            Field(pose, "m_Father", "{fileID: 109339618}"); Field(pose, "m_Children", "[]");
            string visual = Block(scene, 212, renderer);
            Field(visual, "m_GameObject", "{fileID: " + go + "}");
            Field(visual, "m_Enabled", "1"); Field(visual, "m_SortingOrder", "1");
            Field(visual, "m_Sprite", "{fileID: 21300000, guid: " + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite)) + ", type: 3}");
            Field(visual, "m_DrawMode", "0"); Field(visual, "m_Color", "{r: 1, g: 1, b: 1, a: 1}");
            Field(visual, "m_FlipX", "0"); Field(visual, "m_FlipY", "0");
        }

        private static Sprite RequiredSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "Dedicated school-prep asset missing: " + path);
            return sprite;
        }
        private static string ReadScene() => File.ReadAllText(ScenePath).Replace("\r\n", "\n");
        private static string Block(string scene, int type, long id)
        {
            var matches = Regex.Matches(scene, @"(?ms)^--- !u!" + type + " &" + id + @"\n.*?(?=^---|\z)");
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one saved component " + id);
            return matches[0].Value;
        }
        private static string[] Components(string block) => Regex.Matches(block, @"(?m)^  - component: \{fileID: (\d+)\}$").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        private static void Field(string block, string field, string value)
        {
            var matches = Regex.Matches(block, @"(?m)^  " + Regex.Escape(field) + @": ?([^\n]*)$");
            Assert.That(matches.Count, Is.EqualTo(1), "Expected one field " + field);
            Assert.That(matches[0].Groups[1].Value, Is.EqualTo(value), field);
        }
        private static string Sha(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        private static RectInt AlphaBounds(string path)
        {
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                var pixels = raw.GetPixels32();
                var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(visible, Is.Not.Empty);
                int left = visible.Min(i => i % raw.width), right = visible.Max(i => i % raw.width) + 1;
                int bottom = visible.Min(i => i / raw.width), top = visible.Max(i => i / raw.width) + 1;
                return new RectInt(left, bottom, right - left, top - bottom);
            }
            finally { Object.DestroyImmediate(raw); }
        }
        private static uint ColorKey(Color32 p) => ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
