using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode
{
    public sealed class UrbanUiThemeRenderingTests
    {
        private GameObject _root;
        private ScriptableObject _theme;
        private Texture2D _texture;
        private Sprite _panel, _button, _slot;
        private Type ThemeType => typeof(ModernUiTileImage).Assembly.GetType("Rootborn.UI.Modern.UrbanUiThemeDefinition");

        [SetUp] public void SetUp()
        {
            Assert.That(ThemeType, Is.Not.Null, "Urban theme definition must exist.");
            _theme = ScriptableObject.CreateInstance(ThemeType);
            _texture = new Texture2D(64, 64);
            _panel = Sprite.Create(_texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect, Vector4.one * 6);
            _button = Sprite.Create(_texture, new Rect(32, 0, 32, 32), Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect, Vector4.one * 6);
            _slot = Sprite.Create(_texture, new Rect(0, 32, 32, 32), Vector2.one * .5f, 16, 0, SpriteMeshType.FullRect, Vector4.one * 6);
            foreach (var pair in new[] { ("_panelSprite", _panel), ("_buttonSprite", _button), ("_slotSprite", _slot) })
                ThemeType.GetField(pair.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_theme, pair.Item2);
            _root = new GameObject("UrbanTest", typeof(RectTransform));
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_theme);
            UnityEngine.Object.DestroyImmediate(_panel); UnityEngine.Object.DestroyImmediate(_button); UnityEngine.Object.DestroyImmediate(_slot);
            UnityEngine.Object.DestroyImmediate(_texture);
        }
        private ModernUiTileImage Skin()
        {
            var skin = _root.AddComponent<ModernUiTileImage>();
            var setter = typeof(ModernUiTileImage).GetMethod("SetTheme");
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(skin, new object[] { _theme });
            skin.Rebuild();
            return skin;
        }
        [Test] public void Panel_UsesOneSlicedNonRaycastingBackgroundAndReusesIt()
        {
            var content = new GameObject("Label", typeof(RectTransform)); content.transform.SetParent(_root.transform, false);
            var skin = Skin(); var image = _root.transform.Find("[UrbanSkin]").GetComponent<Image>();
            Assert.That(image.sprite, Is.SameAs(_panel)); Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(.5f)); Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.transform.GetSiblingIndex(), Is.Zero);
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 400);
            skin.Rebuild(); _root.SetActive(false); _root.SetActive(true);
            Assert.That(_root.GetComponentsInChildren<Image>().Length, Is.EqualTo(1));
            Assert.That(_root.transform.Find("[UrbanSkin]").GetComponent<Image>(), Is.SameAs(image));
        }
        [TestCase(16f)]
        [TestCase(100f)]
        public void CanvasReferencePpu_PreservesTwoTimesNativeBorder(float referencePpu)
        {
            var canvasRoot = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.referencePixelsPerUnit = referencePpu;
                _root.transform.SetParent(canvasRoot.transform, false);
                Skin(); var image = _root.transform.Find("[UrbanSkin]").GetComponent<Image>();
                float effectivePpu = image.sprite.pixelsPerUnit / canvas.referencePixelsPerUnit * image.pixelsPerUnitMultiplier;
                Assert.That(effectivePpu, Is.EqualTo(.5f).Within(.0001f));
                Assert.That(image.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            }
            finally { _root.transform.SetParent(null); UnityEngine.Object.DestroyImmediate(canvasRoot); }
        }

        [Test] public void ButtonLabels_UseIvoryAndRestoreOriginalOnFallback()
        {
            _root.AddComponent<Image>(); _root.AddComponent<Button>();
            var labelRoot = new GameObject("Label", typeof(RectTransform), typeof(Text)); labelRoot.transform.SetParent(_root.transform, false);
            var label = labelRoot.GetComponent<Text>(); label.color = Color.black;
            var skin = Skin(); Assert.That(label.color.r, Is.GreaterThan(.9f));
            typeof(ModernUiTileImage).GetMethod("SetTheme").Invoke(skin, new object[] { null }); skin.Rebuild();
            Assert.That(label.color, Is.EqualTo(Color.black));
        }

        [Test] public void SlotRecipe_SelectsSlotSprite()
        {
            var skin = Skin(); skin.SetRecipe(ModernUiRecipes.SlotPanel); skin.Rebuild();
            Assert.That(_root.transform.Find("[UrbanSkin]").GetComponent<Image>().sprite, Is.SameAs(_slot));
        }
        [Test] public void Button_KeepsRootHitTargetAndTransitionsOnSkin()
        {
            var hit = _root.AddComponent<Image>(); hit.color = Color.clear;
            var button = _root.AddComponent<Button>(); button.targetGraphic = hit;
            Skin(); var image = _root.transform.Find("[UrbanSkin]").GetComponent<Image>();
            Assert.That(image.sprite, Is.SameAs(_button)); Assert.That(button.targetGraphic, Is.SameAs(image));
            Assert.That(hit.raycastTarget, Is.True); Assert.That(image.raycastTarget, Is.False);
        }
        [Test] public void RemovingInjectedTheme_RestoresLegacyTilesAndButtonGraphic()
        {
            var hit = _root.AddComponent<Image>(); var button = _root.AddComponent<Button>(); button.targetGraphic = hit;
            var skin = Skin();
            typeof(ModernUiTileImage).GetMethod("SetTheme").Invoke(skin, new object[] { null }); skin.Rebuild();
            Assert.That(skin.TileCount, Is.GreaterThan(1)); Assert.That(button.targetGraphic, Is.SameAs(hit));
            Assert.That(_root.transform.Find("[UrbanSkin]").gameObject.activeSelf, Is.False);
        }
        [Test] public void DisabledAndDestroyedSkins_UnsubscribeFromThemeReadiness()
        {
            var runtime = typeof(ModernUiTileImage).Assembly.GetType("Rootborn.UI.Modern.UrbanUiThemeRuntime");
            Assert.That(runtime, Is.Not.Null);
            var field = runtime.GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            int Count() => (field.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
            int before = Count(); var skin = Skin(); Assert.That(Count(), Is.EqualTo(before + 1));
            skin.enabled = false; Assert.That(Count(), Is.EqualTo(before));
            skin.enabled = true; Assert.That(Count(), Is.EqualTo(before + 1));
            UnityEngine.Object.DestroyImmediate(skin); Assert.That(Count(), Is.EqualTo(before));
        }
    }
}
