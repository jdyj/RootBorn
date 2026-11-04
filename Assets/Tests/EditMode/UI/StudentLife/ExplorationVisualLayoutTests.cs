using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    // A real native screenshot exposed content touching the ornamental frame even
    // though its own Text rect and three-line fit passed. Test the containing paper
    // area and generated glyph geometry, not a particular anchoredPosition value.
    public sealed class ExplorationVisualLayoutTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
        private ExplorationChoicePanel _panel;
        private ExplorationInteractionDefinition _definition;
        private StudentLifeProgress _student;
        private ExplorationProgress _progress;
        private Inventory _inventory;

        [SetUp]
        public void SetUp()
        {
            Snapshot(typeof(GameText), "_language");
            foreach (string name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" })
                Snapshot(typeof(GameDataRegistryProvider), name);
            var source = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(source, Is.Not.Null);
            GameDataRegistryProvider.SetOverrideForTests(Own(Object.Instantiate(source)));
            _definition = AssetDatabase.LoadAssetAtPath<ExplorationInteractionDefinition>("Assets/Data/StudentLife/ExplorationInteractions/Exploration_TestAlleyCrate.asset");
            Assert.That(_definition, Is.Not.Null);
            _student = new StudentLifeProgress("visual-layout-fixture", "visual-layout-player", 12, 10, 0, 480);
            _progress = new ExplorationProgress("visual-layout-fixture", "visual-layout-player");
            _inventory = new Inventory();
            var canvas = Own(new GameObject("OwnedExplorationVisualLayoutCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _panel = ExplorationChoicePanel.EnsureInScene(canvas.GetComponent<Canvas>());
            Assert.That(_panel != null && _panel.transform.IsChildOf(canvas.transform), Is.True,
                "The fixture must own its panel; never replace a host scene or hide a foreign canvas.");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_panel != null) _panel.Close();
                for (int i = _owned.Count - 1; i >= 0; i--)
                    if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
                _owned.Clear();
            }
            finally
            {
                foreach (var entry in _statics) entry.Key.SetValue(null, entry.Value);
                _statics.Clear();
            }
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void ThreeLineInspectResultKeepsRectAndGlyphsInsidePaperAndBelowLastChoice(string language)
        {
            // Language assignment is isolated fixture setup, never a live native action.
            GameText.Language = language;
            string before = JsonUtility.ToJson(_student.ToSaveData()) + JsonUtility.ToJson(_progress.ToSaveData());
            string sourceBefore = EditorJsonUtility.ToJson(_definition);
            var model = ExplorationSummaryBuilder.Build(_definition.Id, new ExplorationLookupCache(new[] { _definition }),
                _progress, _student, _inventory, null, null, 1, 480);
            _panel.Open(model, _progress, id => Assert.Fail("Layout must not select an authored choice."));
            // Source-defined Inspect collector order, without executing any domain reward.
            var collector = new ExplorationOutcomeCollector();
            collector.AddItemGrant("Wood:1");
            collector.AddOutcome("item-grant:Wood");
            collector.AddOutcome("trait:trait.focus");
            collector.AddFatigueStatus("stress:1");
            collector.AddFatigueStatus("energy:-1");
            _panel.ShowResult(new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied,
                _definition.Id, "choice.test.inspect", string.Empty, collector));

            var panelRect = (RectTransform)_panel.transform;
            var result = _panel.GetComponentsInChildren<Text>().Single(t => t.name == "ExplorationResultText");
            var leave = _panel.GetComponentsInChildren<Button>().Single(b => b.name == "ChoiceButton_choice.test.leave");
            Rect paper = Rect.MinMaxRect(panelRect.rect.xMin + 24f, panelRect.rect.yMin + 24f,
                panelRect.rect.xMax - 24f, panelRect.rect.yMax - 24f);
            Rect resultRect = InPanel((RectTransform)result.transform, panelRect);
            Rect leaveRect = InPanel((RectTransform)leave.transform, panelRect);
            Rect glyphBounds = GeneratedGlyphBounds(result, panelRect, out int renderedLines);
            float choiceToResultGap = leaveRect.yMin - resultRect.yMax;

            // Compatible sequential assertions; all constraints retained.
            {
                Assert.That(result.text.Split('\n').Length, Is.EqualTo(3), "Exercise the actual three-line Applied presentation.");
                Assert.That(renderedLines, Is.EqualTo(3), "All three lines must actually fit, not be silently truncated.");
                Assert.That(result.preferredHeight, Is.LessThanOrEqualTo(result.rectTransform.rect.height + .5f));
                Inside(resultRect, paper, "The complete result Text rect must stay inside the 24-unit paper inset.");
                Inside(glyphBounds, paper, "Generated visible glyph quads must not touch or cross the ornamental frame.");
                Assert.That(choiceToResultGap, Is.GreaterThanOrEqualTo(16f - .01f),
                    "Raising result content must preserve at least16 logical units below the fourth authored choice.");
                Assert.That(JsonUtility.ToJson(_student.ToSaveData()) + JsonUtility.ToJson(_progress.ToSaveData()), Is.EqualTo(before));
                Assert.That(EditorJsonUtility.ToJson(_definition), Is.EqualTo(sourceBefore));
            }
        }

        private static Rect InPanel(RectTransform child, RectTransform panel)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            return Bounds(corners.Select(panel.InverseTransformPoint).ToArray());
        }

        private static Rect GeneratedGlyphBounds(Text text, RectTransform panel, out int lines)
        {
            Assert.That(text.font, Is.Not.Null);
            using (var generator = new TextGenerator())
            {
                Assert.That(generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size)), Is.True);
                lines = generator.lineCount;
                var vertices = generator.verts;
                Assert.That(vertices.Count, Is.GreaterThanOrEqualTo(4));
                float unitsPerPixel = 1f / text.pixelsPerUnit;
                Vector2 first = new Vector2(vertices[0].position.x, vertices[0].position.y) * unitsPerPixel;
                Vector2 rounding = text.PixelAdjustPoint(first) - first;
                var visible = new List<Vector3>();
                // Current package Text.OnPopulateMesh processes all generated
                // quads. Exclude zero-area whitespace, not an assumed last quad.
                for (int i = 0; i + 3 < vertices.Count; i += 4)
                {
                    var quad = new Vector3[4];
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 point = vertices[i + k].position * unitsPerPixel;
                        point.x += rounding.x; point.y += rounding.y;
                        quad[k] = point;
                    }
                    Rect local = Bounds(quad);
                    if (local.width <= .001f || local.height <= .001f) continue;
                    visible.AddRange(quad.Select(v => panel.InverseTransformPoint(text.transform.TransformPoint(v))));
                }
                Assert.That(visible, Is.Not.Empty, "Measure generated visible glyphs, not merely the allocated Text rect.");
                return Bounds(visible.ToArray());
            }
        }

        private static Rect Bounds(Vector3[] points) => Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        private static void Inside(Rect actual, Rect expected, string message)
        {
            Assert.That(actual.xMin, Is.GreaterThanOrEqualTo(expected.xMin - .01f), message);
            Assert.That(actual.xMax, Is.LessThanOrEqualTo(expected.xMax + .01f), message);
            Assert.That(actual.yMin, Is.GreaterThanOrEqualTo(expected.yMin - .01f), message);
            Assert.That(actual.yMax, Is.LessThanOrEqualTo(expected.yMax + .01f), message);
        }
        private void Snapshot(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            _statics.Add(field, field.GetValue(null));
        }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
    }
}
