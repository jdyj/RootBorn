using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        private GameObject _root;
        private ModernUiTileImage _panelTileImage;

        public bool IsVisible => _root != null && _root.activeSelf;
        public int PanelTileCount => _panelTileImage != null ? _panelTileImage.TileCount : 0;
        public int CornerTileCount => _panelTileImage != null ? _panelTileImage.CornerTileCount : 0;

        public void Show()
        {
            EnsureBuilt();
            _root.SetActive(true);
            _panelTileImage.Rebuild();
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("SettingsPanelRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = new Vector2(512f, 352f);
            _panelTileImage = _root.GetComponent<ModernUiTileImage>();
            _panelTileImage.SetRecipe(ModernUiRecipes.CommonPanel);
            _panelTileImage.Rebuild();

            MakeTitle(rootRt);
            MakeCloseButton(rootRt);
            MakeOptionRow(rootRt, "Sound", new Vector2(-192f, 78f), true);
            MakeOptionRow(rootRt, "Music", new Vector2(-192f, 22f), true);
            MakeSliderRow(rootRt, "Text", new Vector2(-192f, -34f));
            MakeOptionRow(rootRt, "Fullscreen", new Vector2(-192f, -90f), false);
        }

        private static void MakeTitle(RectTransform parent)
        {
            var tab = new GameObject("TitleTab", typeof(RectTransform), typeof(ModernUiTileImage));
            tab.transform.SetParent(parent, false);
            var rt = (RectTransform)tab.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -16f);
            rt.sizeDelta = new Vector2(176f, 48f);
            var tiles = tab.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            MakeText(rt, "Settings", 24, TextAnchor.MiddleCenter, Vector2.zero, rt.sizeDelta);
        }

        private void MakeCloseButton(RectTransform parent)
        {
            var buttonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            buttonGo.transform.SetParent(parent, false);
            var rt = (RectTransform)buttonGo.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(48f, 48f);
            buttonGo.GetComponent<Image>().color = Color.clear;
            var tiles = buttonGo.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            buttonGo.GetComponent<Button>().onClick.AddListener(Hide);
            MakeText(rt, "X", 20, TextAnchor.MiddleCenter, Vector2.zero, rt.sizeDelta);
        }

        private static void MakeOptionRow(RectTransform parent, string label, Vector2 position, bool isOn)
        {
            var row = MakeRow(parent, label, position);
            var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(ModernUiTileImage));
            toggleGo.transform.SetParent(row, false);
            var rt = (RectTransform)toggleGo.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(64f, 32f);
            toggleGo.GetComponent<Image>().color = Color.clear;
            toggleGo.GetComponent<ModernUiTileImage>().Rebuild();
            toggleGo.GetComponent<Toggle>().isOn = isOn;
        }

        private static void MakeSliderRow(RectTransform parent, string label, Vector2 position)
        {
            var row = MakeRow(parent, label, position);
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var rt = (RectTransform)sliderGo.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(160f, 24f);
            sliderGo.GetComponent<Slider>().value = 0.6f;
        }

        private static RectTransform MakeRow(RectTransform parent, string label, Vector2 position)
        {
            var row = new GameObject(label + "Row", typeof(RectTransform), typeof(ModernUiTileImage));
            row.transform.SetParent(parent, false);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(384f, 44f);
            row.GetComponent<ModernUiTileImage>().Rebuild();
            MakeText(rt, label, 18, TextAnchor.MiddleLeft, new Vector2(18f, 0f), new Vector2(180f, 40f));
            return rt;
        }

        private static Text MakeText(RectTransform parent, string value, int fontSize, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = textGo.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            return text;
        }
    }
}
