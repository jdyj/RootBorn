using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public static class ModernUiWindowBuilder
    {
        public static GameObject BuildInventoryPreview(Transform parent)
        {
            var root = MakeWindow(parent, "InventoryPreviewWindow", new Vector2(-280f, 0f), new Vector2(448f, 384f), "Inventory");
            var rect = (RectTransform)root.transform;
            MakeTilePanel(rect, "HeaderPanel", new Vector2(0f, 118f), new Vector2(304f, 48f));
            MakeText(rect, "Search", new Vector2(-100f, 118f), new Vector2(160f, 32f), 16, TextAnchor.MiddleLeft);
            MakeSlotGrid(rect, new Vector2(-160f, 48f), 5, 4);
            MakeTilePanel(rect, "Scrollbar", new Vector2(188f, -12f), new Vector2(32f, 256f));
            MakeTilePanel(rect, "TrashButton", new Vector2(128f, 118f), new Vector2(48f, 48f));
            return root;
        }

        public static GameObject BuildStatusPreview(Transform parent)
        {
            var root = MakeWindow(parent, "StatusPreviewWindow", new Vector2(280f, 0f), new Vector2(448f, 384f), "Status");
            var rect = (RectTransform)root.transform;
            MakeTilePanel(rect, "PortraitFrame", new Vector2(-128f, 56f), new Vector2(112f, 144f));
            MakeText(rect, "Charlie", new Vector2(-128f, 140f), new Vector2(120f, 28f), 16, TextAnchor.MiddleCenter);
            MakeGauge(rect, "Energy", new Vector2(56f, 96f));
            MakeGauge(rect, "Health", new Vector2(56f, 48f));
            MakeGauge(rect, "Focus", new Vector2(56f, 0f));
            MakeGauge(rect, "Hunger", new Vector2(56f, -48f));
            MakeSlotGrid(rect, new Vector2(-160f, -138f), 7, 1);
            return root;
        }

        private static GameObject MakeWindow(Transform parent, string name, Vector2 position, Vector2 size, string title)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var tiles = root.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            MakeTilePanel(rt, "TitleTab", new Vector2(0f, size.y * 0.5f - 28f), new Vector2(160f, 48f));
            MakeText(rt, title, new Vector2(0f, size.y * 0.5f - 30f), new Vector2(140f, 32f), 20, TextAnchor.MiddleCenter);
            return root;
        }

        private static void MakeSlotGrid(RectTransform parent, Vector2 start, int columns, int rows)
        {
            const float cell = 48f;
            const float gap = 8f;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    MakeTilePanel(parent, $"Slot_{y}_{x}", new Vector2(start.x + x * (cell + gap), start.y - y * (cell + gap)), new Vector2(cell, cell));
                }
            }
        }

        private static void MakeGauge(RectTransform parent, string label, Vector2 position)
        {
            MakeText(parent, label, new Vector2(position.x - 134f, position.y), new Vector2(96f, 26f), 14, TextAnchor.MiddleRight);
            MakeTilePanel(parent, label + "Gauge", position, new Vector2(176f, 28f));
            MakeTilePanel(parent, label + "Fill", new Vector2(position.x - 34f, position.y), new Vector2(88f, 16f));
        }

        private static void MakeTilePanel(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
        }

        private static Text MakeText(RectTransform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var textGo = new GameObject(value + "Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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
