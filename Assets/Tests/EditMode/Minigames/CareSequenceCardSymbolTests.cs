using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Minigames;
using Rootborn.UI.Minigames;
using Rootborn.UI.Modern;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Minigames
{
    public sealed class CareSequenceCardSymbolTests
    {
        private const string PhonePath = "Assets/Art/CareerMinigames/Comfy31/care_call_handset.png";
        private const string SearchPath = "Assets/Art/UI/UrbanMolding/inventory_search.png";
        private const string CasePath = "Assets/Art/CareerMinigames/Comfy31/first_aid_case.png";
        private static readonly string[] SymbolKeys = { "minigame.care.check", "minigame.care.call", "minigame.care.help" };
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _providerState = new Dictionary<FieldInfo, object>();
        private MinigamePanel _panel;

        [SetUp]
        public void SetUp()
        {
            foreach (var name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" })
            {
                var field = typeof(GameDataRegistryProvider).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Fixture provider isolation seam changed: " + name);
                _providerState.Add(field, field.GetValue(null));
            }
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            GameDataRegistryProvider.SetOverrideForTests(registry);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null) _panel.Close();
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            foreach (var pair in _providerState) pair.Key.SetValue(null, pair.Value);
            _providerState.Clear();
        }

        [Test]
        public void HandsetUsesStandardFullRectImportAddressAndCrispTwoPixelSourceArt()
        {
            SpriteAt(PhonePath, 48, 48f, new Vector2(24f, 24f));
            var importer = (TextureImporter)AssetImporter.GetAtPath(PhonePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(PhonePath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo("sprites/minigame/care-call-handset"));
            Assert.That(entry.labels, Does.Contain("CareerMinigames"));
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(PhonePath)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(48, 48)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.InRange(1, 31));
                for (int y = 0; y < 48; y += 2)
                    for (int x = 0; x < 48; x += 2)
                    {
                        uint expected = ColorKey(pixels[y * 48 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 48 + x + 1]), ColorKey(pixels[(y + 1) * 48 + x]), ColorKey(pixels[(y + 1) * 48 + x + 1]) },
                            Is.All.EqualTo(expected), "Nonuniform handset2x2 cluster at " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void ReusedSearchAndFirstAidCaseRetainTheirDifferentOriginalSpriteGeometry()
        {
            var search = SpriteAt(SearchPath, 32, 16f, new Vector2(16f, 16f));
            SpriteAt(CasePath, 48, 48f, new Vector2(24f, 24f));
            var theme = AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.GetIcon("inventory-search"), Is.SameAs(search));
            // Existing first-aid art is reused as authored. Exact2px/color constraints apply only to the new handset.
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AuthoredCareSequenceMapsSearchPhoneCaseWithoutChangingRules(int level)
        {
            var challenge = Challenge(level);
            var data = new SerializedObject(challenge);
            Assert.That(data.FindProperty("_instructionKey").stringValue, Is.EqualTo("minigame.care.sequence"));
            Assert.That(data.FindProperty("_timeLimitSeconds").floatValue, Is.EqualTo(25f));
            Assert.That(data.FindProperty("_mistakeAllowance").intValue, Is.EqualTo(6));
            Assert.That(data.FindProperty("_revealSeconds").floatValue, Is.EqualTo(3f));
            var answers = data.FindProperty("_answers");
            Assert.That(Enumerable.Range(0, answers.arraySize).Select(i => answers.GetArrayElementAtIndex(i).intValue), Is.EqualTo(new[] { 0, 1, 2 }));
            var symbols = data.FindProperty("_symbols");
            Assert.That(Enumerable.Range(0, symbols.arraySize).Select(i => symbols.GetArrayElementAtIndex(i).stringValue), Is.EqualTo(SymbolKeys));
            var icons = data.FindProperty("_symbolSprites");
            Assert.That(icons.arraySize, Is.EqualTo(3));
            var expected = ExpectedSprites();
            for (int i = 0; i < expected.Length; i++)
                Assert.That(icons.GetArrayElementAtIndex(i).objectReferenceValue, Is.SameAs(expected[i]), SymbolKeys[i]);
            Assert.That(challenge.IsValid, Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AuthoredCareCardsPreviewInOrderHideForRecallAndCompleteWithoutChangingTheirSource(int level)
        {
            var challenge = Challenge(level);
            // Compare the complete serialized source opaquely; do not assume an EditorJsonUtility root shape.
            string before = EditorJsonUtility.ToJson(challenge);
            var game = ScriptableObject.CreateInstance<MinigameDefinition>();
            _owned.Add(game);
            var data = new SerializedObject(game);
            var stages = data.FindProperty("_challenges");
            stages.arraySize = 1; stages.GetArrayElementAtIndex(0).objectReferenceValue = challenge;
            data.ApplyModifiedPropertiesWithoutUndo();
            var session = new MinigameSession(game, null, 123);
            var canvas = new GameObject("CareCardUnitCanvas", typeof(RectTransform), typeof(Canvas));
            _owned.Add(canvas);
            _panel = MinigamePanel.EnsureInScene(canvas.GetComponent<Canvas>());
            int deliveries = 0;
            Assert.That(_panel.Show(session, delivered => { deliveries++; Assert.That(delivered, Is.SameAs(session)); }), Is.True);
            Assert.That(session.Current.Duration, Is.EqualTo(25f));
            Assert.That(session.Current.MistakeAllowance, Is.EqualTo(6));
            Assert.That(session.Current.PreviewVisible, Is.True);
            Assert.That(session.Current.PreviewSequence, Is.EqualTo(new[] { 0, 1, 2 }));
            var expected = ExpectedSprites();
            for (int i = 0; i < expected.Length; i++)
            {
                var preview = Find("PreviewSymbol_" + i).GetComponent<Image>();
                var choice = Choice(i);
                var icon = choice.transform.Find("SymbolIcon").GetComponent<Image>();
                Assert.That(preview.sprite, Is.SameAs(expected[i]));
                Assert.That(preview.gameObject.activeInHierarchy, Is.True);
                Assert.That(preview.rectTransform.sizeDelta, Is.EqualTo(new Vector2(40, 40)));
                Assert.That(preview.raycastTarget, Is.False);
                Assert.That(preview.preserveAspect, Is.True);
                Assert.That(icon.sprite, Is.SameAs(expected[i]));
                Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(new Vector2(48, 48)));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(choice.interactable, Is.False);
                Assert.That(choice.GetComponentInChildren<Text>().text, Is.EqualTo(GameText.Get(SymbolKeys[i])));
            }
            Choice(0).onClick.Invoke();
            Assert.That(session.Current.ProgressIndex, Is.Zero, "Preview input must not submit a recall answer.");
            session.AdvanceTime(3f); // Isolated unit clock; native verification must wait through the actual three-second preview.
            _panel.RefreshView();
            Assert.That(session.Current.PreviewVisible, Is.False);
            Assert.That(session.Current.RemainingSeconds, Is.EqualTo(22f));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Find("PreviewSymbol_" + i).gameObject.activeInHierarchy, Is.False);
                Assert.That(Choice(i).interactable, Is.True);
                Assert.That(Choice(i).transform.Find("SymbolIcon").GetComponent<Image>().sprite, Is.SameAs(expected[i]));
            }
            Choice(0).onClick.Invoke();
            Assert.That(session.Current.ProgressIndex, Is.EqualTo(1));
            Choice(1).onClick.Invoke();
            Assert.That(session.Current.ProgressIndex, Is.EqualTo(2));
            Choice(2).onClick.Invoke();
            Assert.That(session.IsComplete && session.Succeeded, Is.True);
            Assert.That(session.Score, Is.EqualTo(100));
            Assert.That(session.Current.Mistakes, Is.Zero);
            Assert.That(deliveries, Is.EqualTo(1));
            _panel.Close(); _panel.Close();
            Assert.That(deliveries, Is.EqualTo(1));
            Assert.That(EditorJsonUtility.ToJson(challenge), Is.EqualTo(before));
        }

        private static Sprite SpriteAt(string path, int size, float ppu, Vector2 pivot)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, size, size)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(ppu));
            Assert.That(sprite.pivot, Is.EqualTo(pivot));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            return sprite;
        }
        private static Sprite[] ExpectedSprites() => new[] {
            SpriteAt(SearchPath, 32, 16f, new Vector2(16f, 16f)),
            SpriteAt(PhonePath, 48, 48f, new Vector2(24f, 24f)),
            SpriteAt(CasePath, 48, 48f, new Vector2(24f, 24f)) };
        private static SequenceChallengeDefinition Challenge(int level)
        {
            var challenge = AssetDatabase.LoadAssetAtPath<SequenceChallengeDefinition>("Assets/Data/StudentLife/Minigames/Challenges/CareSequence" + level + ".asset");
            Assert.That(challenge, Is.Not.Null);
            return challenge;
        }
        private Transform Find(string name)
        {
            var child = _panel.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            Assert.That(child, Is.Not.Null, "Missing care-card presentation: " + name);
            return child;
        }
        private Button Choice(int index) => Find("Choice_" + index).GetComponent<Button>();
        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
