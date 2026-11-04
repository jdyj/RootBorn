using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.House
{
    public sealed class HouseDailyPlanPromptPlacementTests
    {
        const string House = "Assets/Scenes/House.unity";
        const string Field = "_interactionPromptOffset";
        const string Art = "Assets/Art/HousePixels/SchoolPrep31/";

        [TestCase(714)]
        [TestCase(540)]
        public void AuthoredHousePromptEnvelopeClearsBothOpaquePropsByEightPixels(int pixelHeight)
        {
            string scene = File.ReadAllText(House);
            var go = new GameObject("OwnedHousePromptGeometryTest");
            try
            {
                var board = go.AddComponent<DailyPlanBoardInteractor>();
                ApplyAuthoredOffsetIfPresent(board, Block(scene, 109339619));
                Vector3 offset = board.InteractionPromptOffset;
                Assert.That(offset.x, Is.Zero, "The House fix must not change the authored horizontal anchor.");
                Assert.That(offset.z, Is.Zero, "The House fix must not change prompt depth.");
                float boardY = Vector(Block(scene, 109339618), "m_LocalPosition").y;
                Assert.That(boardY, Is.EqualTo(-2.5f));
                float top = Mathf.Max(VisibleTop(scene, 2073228313, "school_prep_notebook"), VisibleTop(scene, 2055273353, "school_prep_name_tag"));
                // Published presentation budget: an existing router-fitted prompt is at most
                // 24 screen pixels tall. Native rendered mesh clearance is checked separately.
                float gap = (boardY + offset.y - top) * pixelHeight / 10f - 12f;
                Assert.That(gap, Is.GreaterThanOrEqualTo(8f), "Existing House prompt overlaps the accepted notebook/name-tag art. Preserve art geometry; lift only the House anchor.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HouseAndSchoolOwnReviewedOverridesWhileTownKeepsDefault()
        {
            var go = new GameObject("OwnedHousePromptSerializationTest");
            try
            {
                var board = go.AddComponent<DailyPlanBoardInteractor>();
                var property = new SerializedObject(board).FindProperty(Field);
                Assert.That(property, Is.Not.Null, "Prompt placement must be authorable per instance, preserving the shared default.");
                string body = Block(File.ReadAllText(House), 109339619);
                Assert.That(Vector(body, Field), Is.EqualTo(new Vector3(0f, 1.7f, 0f)));
                Assert.That(Vector(Block(File.ReadAllText("Assets/Scenes/School.unity"), 224293929), Field), Is.EqualTo(new Vector3(0f, 1.6f, 0f)), "School owns its separately reviewed prompt override.");
                foreach (string path in new[] { "Assets/Scenes/Town.unity" })
                    Assert.That(Regex.IsMatch(File.ReadAllText(path), @"(?m)^  " + Field + @":"), Is.False, "Town must retain the shared unconfigured prompt offset: " + path);
                ApplyAuthoredOffsetIfPresent(board, body);
                Assert.That(board.InteractionPromptOffset, Is.EqualTo(new Vector3(0f, 1.7f, 0f)), "The public interaction contract must use the serialized instance value.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void UnconfiguredSchoolOrRuntimeBoardKeepsOriginalOffsetPriorityAndText()
        {
            var go = new GameObject("OwnedUnconfiguredDailyPlanBoard");
            try
            {
                var board = go.AddComponent<DailyPlanBoardInteractor>();
                Assert.That(board.InteractionPromptOffset, Is.EqualTo(new Vector3(0f, 1.1f, 0f)));
                Assert.That(board.InteractionPriority, Is.EqualTo(80));
                Assert.That(board.InteractionPrompt, Is.EqualTo("[E] Plan Day"));
                Assert.That(board.InteractionTransform, Is.SameAs(go.transform));
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void ApplyAuthoredOffsetIfPresent(DailyPlanBoardInteractor board, string body)
        {
            if (!Regex.IsMatch(body, @"(?m)^  " + Field + @":")) return;
            var serialized = new SerializedObject(board);
            var property = serialized.FindProperty(Field);
            Assert.That(property, Is.Not.Null, "Authored field must exist on the actual component.");
            property.vector3Value = Vector(body, Field);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static string Block(string text, long id)
        {
            var match = Regex.Match(text.Replace("\r\n", "\n"), @"(?ms)^--- !u!\d+ &" + id + @"\n.*?(?=^--- !u!|\z)");
            Assert.That(match.Success, Is.True, "Missing exact authored House block " + id);
            return match.Value;
        }
        static Vector3 Vector(string body, string field)
        {
            var match = Regex.Match(body, @"(?m)^  " + Regex.Escape(field) + @": \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");
            Assert.That(match.Success, Is.True, "Missing authored vector " + field);
            return new Vector3(Parse(match.Groups[1].Value), Parse(match.Groups[2].Value), Parse(match.Groups[3].Value));
        }
        static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        static float VisibleTop(string scene, long transformId, string name)
        {
            string path = Art + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "Accepted House prop required.");
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                var pixels = raw.GetPixels32();
                var occupied = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).ToArray();
                Assert.That(occupied, Is.Not.Empty);
                float topPixel = occupied.Max(i => i / raw.width) + 1;
                string body = Block(scene, transformId);
                Assert.That(body, Does.Contain("m_Father: {fileID: 109339618}"));
                float boardY = Vector(Block(scene, 109339618), "m_LocalPosition").y;
                return boardY + Vector(body, "m_LocalPosition").y + (topPixel - sprite.pivot.y) / sprite.pixelsPerUnit * Vector(body, "m_LocalScale").y;
            }
            finally { Object.DestroyImmediate(raw); }
        }
    }
}
