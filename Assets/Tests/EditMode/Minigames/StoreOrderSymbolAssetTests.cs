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
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Minigames
{
    public sealed class StoreOrderSymbolAssetTests
    {
        private const string Root = "Assets/Art/CareerMinigames/Comfy31/";
        private const string ChallengePath = "Assets/Data/StudentLife/Minigames/Challenges/StoreIntro.asset";
        private static readonly string[] Names = { "store_bread", "store_milk", "store_fruit" };
        private static readonly string[] SymbolKeys = { "minigame.goods.bread", "minigame.goods.milk", "minigame.goods.fruit" };
        private readonly Dictionary<FieldInfo, object> _providerState = new Dictionary<FieldInfo, object>();
        private readonly List<Object> _owned = new List<Object>();
        private MinigamePanel _panel;

        [SetUp]
        public void SetUp()
        {
            foreach (var name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" })
            {
                var field = typeof(GameDataRegistryProvider).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Fixture provider seam changed: " + name);
                _providerState.Add(field, field.GetValue(null));
            }
            GameDataRegistryProvider.SetOverrideForTests(AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset"));
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

        [TestCase("store_bread")]
        [TestCase("store_milk")]
        [TestCase("store_fruit")]
        public void StoreSymbolUsesStandardImportAddressAndCrispTwoPixelSourceArt(string name)
        {
            string path = Root + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "Dedicated store-order symbol missing: " + path);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 48, 48)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(24f, 24f)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo("sprites/minigame/" + name.Replace('_', '-')));
            Assert.That(entry.labels, Does.Contain("CareerMinigames"));
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(raw.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(raw.width, raw.height), Is.EqualTo(new Vector2Int(48, 48)));
                var pixels = raw.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True, name + " contains softened alpha.");
                Assert.That(new HashSet<uint>(pixels.Where(p => p.a != 0).Select(ColorKey)).Count, Is.InRange(1, 31));
                for (int y = 0; y < 48; y += 2)
                    for (int x = 0; x < 48; x += 2)
                    {
                        uint color = ColorKey(pixels[y * 48 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 48 + x + 1]), ColorKey(pixels[(y + 1) * 48 + x]), ColorKey(pixels[(y + 1) * 48 + x + 1]) },
                            Is.All.EqualTo(color), name + " nonuniform2x2 cluster at " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(raw); }
        }

        [Test]
        public void StoreIntroMapsBreadMilkFruitInOrderWithoutChangingItsAuthoredRules()
        {
            var challenge = Challenge();
            var data = new SerializedObject(challenge);
            Assert.That(data.FindProperty("_instructionKey").stringValue, Is.EqualTo("minigame.store.instructions"));
            Assert.That(data.FindProperty("_timeLimitSeconds").floatValue, Is.EqualTo(35f));
            Assert.That(data.FindProperty("_mistakeAllowance").intValue, Is.EqualTo(6));
            Assert.That(data.FindProperty("_revealSeconds").floatValue, Is.EqualTo(4f));
            var answers = data.FindProperty("_answers");
            Assert.That(Enumerable.Range(0, answers.arraySize).Select(i => answers.GetArrayElementAtIndex(i).intValue), Is.EqualTo(new[] { 0, 1, 2 }));
            var symbols = data.FindProperty("_symbols");
            Assert.That(Enumerable.Range(0, symbols.arraySize).Select(i => symbols.GetArrayElementAtIndex(i).stringValue), Is.EqualTo(SymbolKeys));
            var sprites = data.FindProperty("_symbolSprites");
            Assert.That(sprites.arraySize, Is.EqualTo(3));
            var expected = ExpectedSprites();
            for (int i = 0; i < expected.Length; i++)
                Assert.That(sprites.GetArrayElementAtIndex(i).objectReferenceValue, Is.SameAs(expected[i]), SymbolKeys[i]);
            Assert.That(challenge.IsValid, Is.True);
        }

        [Test]
        public void AuthoredStorePreviewShowsThreeSymbolsInAnswerOrderBesideLocalizedChoiceLabels()
        {
            var challenge = Challenge();
            string before = EditorJsonUtility.ToJson(challenge);
            var session = Session(challenge);
            Show(session, _ => { });
            var expected = ExpectedSprites();
            Assert.That(session.Current.PreviewVisible, Is.True);
            Assert.That(session.Current.PreviewSequence, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(session.Current.Duration, Is.EqualTo(35f));
            Assert.That(session.Current.MistakeAllowance, Is.EqualTo(6));
            for (int i = 0; i < expected.Length; i++)
            {
                var preview = Find("PreviewSymbol_" + i).GetComponent<Image>();
                Assert.That(preview.gameObject.activeInHierarchy, Is.True);
                Assert.That(preview.sprite, Is.SameAs(expected[i]));
                Assert.That(preview.preserveAspect, Is.True);
                Assert.That(preview.raycastTarget, Is.False);
                var choice = Choice(i);
                var icon = choice.transform.Find("SymbolIcon").GetComponent<Image>();
                Assert.That(icon.sprite, Is.SameAs(expected[i]));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(choice.interactable, Is.False);
                Assert.That(choice.GetComponentInChildren<Text>().text, Is.EqualTo(GameText.Get(SymbolKeys[i])));
            }
            Assert.That(EditorJsonUtility.ToJson(challenge), Is.EqualTo(before));
        }

        [Test]
        public void RecallHidesThePreviewAndExistingChoiceButtonsCompleteExactlyOneUnchangedStoreSession()
        {
            var challenge = Challenge();
            string before = EditorJsonUtility.ToJson(challenge);
            var session = Session(challenge);
            int deliveries = 0;
            Show(session, result => { deliveries++; Assert.That(result, Is.SameAs(session)); });
            Choice(0).onClick.Invoke();
            Assert.That(session.Current.ProgressIndex, Is.Zero, "A preview click must not answer before memorization ends.");
            session.AdvanceTime(4f); // Unit fixture clock only; native evidence must wait through the real preview.
            _panel.RefreshView();
            Assert.That(session.Current.PreviewVisible, Is.False);
            Assert.That(session.Current.RemainingSeconds, Is.EqualTo(31f));
            var expected = ExpectedSprites();
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

        private static SequenceChallengeDefinition Challenge()
        {
            var challenge = AssetDatabase.LoadAssetAtPath<SequenceChallengeDefinition>(ChallengePath);
            Assert.That(challenge, Is.Not.Null);
            return challenge;
        }
        private static Sprite[] ExpectedSprites()
        {
            return Names.Select(name => {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + name + ".png");
                Assert.That(sprite, Is.Not.Null, name);
                return sprite;
            }).ToArray();
        }
        private MinigameSession Session(SequenceChallengeDefinition challenge)
        {
            var game = ScriptableObject.CreateInstance<MinigameDefinition>();
            _owned.Add(game);
            var data = new SerializedObject(game);
            var stages = data.FindProperty("_challenges");
            stages.arraySize = 1; stages.GetArrayElementAtIndex(0).objectReferenceValue = challenge;
            data.ApplyModifiedPropertiesWithoutUndo();
            return new MinigameSession(game, null, 123);
        }
        private void Show(MinigameSession session, Action<MinigameSession> complete)
        {
            var root = new GameObject("StoreOrderUnitCanvas", typeof(RectTransform), typeof(Canvas));
            _owned.Add(root);
            _panel = MinigamePanel.EnsureInScene(root.GetComponent<Canvas>());
            Assert.That(_panel.Show(session, complete), Is.True);
        }
        private Transform Find(string name)
        {
            var child = _panel.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            Assert.That(child, Is.Not.Null, "Missing authored StoreIntro presentation: " + name);
            return child;
        }
        private Button Choice(int index) => Find("Choice_" + index).GetComponent<Button>();
        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
    }
}
