using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    [DisallowMultipleComponent]
    public sealed class ModernUiStatusPanel : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(480f, 380f);
        private bool _built;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public int TileCount
        {
            get
            {
                int count = 0;
                var tiles = GetComponentsInChildren<ModernUiTileImage>(true);
                for (int i = 0; i < tiles.Length; i++)
                {
                    count += tiles[i].TileCount;
                }

                return count;
            }
        }

        public void Show()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            _isVisible = true;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _isVisible = false;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta == Vector2.zero)
            {
                rect.sizeDelta = DefaultSize;
            }

            MakeTilePanel("StatusTitleTab", new Vector2(0f, 154f), new Vector2(188f, 42f));
            MakeTilePanel("PortraitFrame", new Vector2(-128f, 52f), new Vector2(128f, 164f));
            MakeTilePanel("StatusValueFrame", new Vector2(88f, 78f), new Vector2(216f, 88f));
            MakeGauge("EnergyGauge", "ENERGY", new Vector2(88f, 2f));
            MakeGauge("HealthGauge", "HEALTH", new Vector2(88f, -54f));
            MakeTilePanel("IconFrame", new Vector2(-128f, -124f), new Vector2(128f, 54f));
            MakeTilePanel("StatusBottomButtons", new Vector2(88f, -138f), new Vector2(216f, 50f));
            MakeLabel((RectTransform)transform, "StatusTitle", "STATUS", new Vector2(0f, 154f), new Vector2(168f, 28f), 18, TextAnchor.MiddleCenter);
            MakeLabel((RectTransform)transform, "CharacterName", "ROOTBORN", new Vector2(26f, 104f), new Vector2(160f, 24f), 14, TextAnchor.UpperLeft);
            _built = true;
        }

        private void MakeGauge(string name, string label, Vector2 position)
        {
            MakeLabel((RectTransform)transform, name + "Label", label, new Vector2(position.x - 146f, position.y + 11f), new Vector2(96f, 22f), 12, TextAnchor.MiddleRight);
            MakeTilePanel(name, position, new Vector2(216f, 34f));
            MakeTilePanel(name + "Fill", new Vector2(position.x - 40f, position.y), new Vector2(112f, 16f));
        }

        private GameObject MakeTilePanel(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            return go;
        }

        private static Text MakeLabel(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.28f, 0.19f, 0.12f, 1f);
            return text;
        }
    }
}
