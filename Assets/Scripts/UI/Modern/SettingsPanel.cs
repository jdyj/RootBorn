using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static readonly Color FrameDark = new Color(0.38f, 0.26f, 0.24f, 1f);
        private static readonly Color FrameMid = new Color(0.59f, 0.45f, 0.37f, 1f);
        private static readonly Color PanelFill = new Color(0.75f, 0.65f, 0.52f, 1f);
        private static readonly Color PanelLight = new Color(0.87f, 0.76f, 0.62f, 1f);
        private static readonly Color PanelShadow = new Color(0.44f, 0.33f, 0.30f, 1f);
        private static readonly Color AccentBlue = new Color(0.37f, 0.37f, 0.55f, 1f);
        private static readonly Color AccentGreen = new Color(0.18f, 0.55f, 0.34f, 1f);
        private static readonly Color AccentRed = new Color(0.77f, 0.20f, 0.18f, 1f);
        private static readonly Color AccentGold = new Color(0.84f, 0.57f, 0.28f, 1f);

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
            rootRt.sizeDelta = new Vector2(660f, 510f);
            _panelTileImage = _root.GetComponent<ModernUiTileImage>();
            _panelTileImage.SetRecipe(ModernUiRecipes.CommonPanel);
            _panelTileImage.Rebuild();

            BuildReferenceLikePanel(rootRt);
        }

        private void BuildReferenceLikePanel(RectTransform root)
        {
            MakeRect(root, "BaseFill", Vector2.zero, new Vector2(660f, 510f), PanelFill);
            MakeRect(root, "OuterBorderTop", new Vector2(0f, 230f), new Vector2(664f, 16f), FrameDark);
            MakeRect(root, "OuterBorderBottom", new Vector2(0f, -230f), new Vector2(664f, 16f), FrameDark);
            MakeRect(root, "OuterBorderLeft", new Vector2(-326f, 0f), new Vector2(16f, 456f), FrameDark);
            MakeRect(root, "OuterBorderRight", new Vector2(326f, 0f), new Vector2(16f, 456f), FrameDark);
            MakeRect(root, "InnerHighlight", new Vector2(0f, 0f), new Vector2(604f, 390f), PanelLight);
            MakeRect(root, "InnerFill", new Vector2(0f, -6f), new Vector2(574f, 348f), PanelFill);

            MakeTopTabs(root);
            MakeSideRail(root, -252f);
            MakeSideRail(root, 252f);
            MakeScrollbar(root);
            MakeSettingsGroup(root, new Vector2(0f, 58f));
            MakeIconGroup(root, new Vector2(0f, -126f));
            MakeCornerOrnaments(root);
            MakePatternRow(root, 154f);
            MakePatternRow(root, -204f);
        }

        private static void MakeTopTabs(RectTransform root)
        {
            float[] xPositions = { -228f, -112f, 0f, 112f, 228f };
            Color[] colors = { AccentGold, AccentGreen, AccentRed, AccentBlue, PanelLight };
            for (int i = 0; i < xPositions.Length; i++)
            {
                MakeRect(root, "TopTab" + i, new Vector2(xPositions[i], 220f), new Vector2(92f, 70f), FrameDark);
                MakeRect(root, "TopTabFill" + i, new Vector2(xPositions[i], 218f), new Vector2(72f, 50f), PanelFill);
                MakeRect(root, "TopTabIcon" + i, new Vector2(xPositions[i], 218f), new Vector2(38f, 30f), colors[i]);
            }

            MakeGear(root, new Vector2(228f, 218f));
        }

        private static void MakeSettingsGroup(RectTransform root, Vector2 center)
        {
            MakeRect(root, "SettingsGroupBorder", center, new Vector2(386f, 136f), FrameDark);
            MakeRect(root, "SettingsGroupFill", center, new Vector2(356f, 102f), PanelLight);
            MakeRect(root, "SettingsGroupInset", center, new Vector2(328f, 82f), PanelFill);

            MakeSpeakerIcon(root, center + new Vector2(-132f, 26f));
            MakeToggle(root, center + new Vector2(-38f, 25f), true);
            MakeMutedIcon(root, center + new Vector2(42f, 26f));
            MakeToggle(root, center + new Vector2(132f, 25f), false);
            MakeMusicIcon(root, center + new Vector2(-132f, -32f));
            MakeToggle(root, center + new Vector2(-38f, -32f), false);
            MakeTextGlyph(root, center + new Vector2(42f, -32f));
            MakeToggle(root, center + new Vector2(132f, -32f), true);
        }

        private static void MakeIconGroup(RectTransform root, Vector2 center)
        {
            MakeRect(root, "IconGroupBorder", center, new Vector2(386f, 116f), FrameDark);
            MakeRect(root, "IconGroupFill", center, new Vector2(356f, 82f), PanelLight);
            MakeRect(root, "IconGroupInset", center, new Vector2(328f, 62f), PanelFill);

            for (int i = 0; i < 5; i++)
            {
                Vector2 slot = center + new Vector2(-128f + i * 64f, 0f);
                MakeRect(root, "IconButton" + i, slot, new Vector2(50f, 50f), FrameMid);
                MakeRect(root, "IconButtonFill" + i, slot, new Vector2(34f, 34f), i == 0 ? Color.white : AccentBlue);
            }

            MakeCursor(root, center + new Vector2(-128f, -2f));
        }

        private static void MakeSideRail(RectTransform root, float x)
        {
            MakeRect(root, "SideRail" + x, new Vector2(x, -22f), new Vector2(62f, 292f), FrameMid);
            MakeRect(root, "SideRailInset" + x, new Vector2(x, -22f), new Vector2(38f, 250f), PanelFill);
            for (int i = 0; i < 5; i++)
            {
                MakeFlower(root, new Vector2(x, 82f - i * 48f));
            }
        }

        private static void MakeScrollbar(RectTransform root)
        {
            MakeRect(root, "ScrollTrack", new Vector2(294f, -22f), new Vector2(18f, 292f), FrameDark);
            MakeRect(root, "ScrollFill", new Vector2(294f, -22f), new Vector2(8f, 244f), PanelShadow);
            MakeRect(root, "ScrollThumb", new Vector2(294f, 118f), new Vector2(32f, 24f), FrameMid);
        }

        private static void MakeToggle(RectTransform root, Vector2 center, bool on)
        {
            MakeRect(root, "ToggleTrack", center, new Vector2(78f, 36f), PanelShadow);
            MakeRect(root, "ToggleTrackFill", center, new Vector2(58f, 22f), on ? AccentBlue : FrameMid);
            MakeRect(root, "ToggleKnob", center + new Vector2(on ? -18f : 18f, 0f), new Vector2(36f, 36f), AccentGold);
            MakeRect(root, "ToggleKnobLight", center + new Vector2(on ? -18f : 18f, 5f), new Vector2(22f, 14f), PanelLight);
        }

        private static void MakeGear(RectTransform root, Vector2 center)
        {
            MakeRect(root, "GearCore", center, new Vector2(30f, 30f), AccentBlue);
            MakeRect(root, "GearToothA", center + new Vector2(0f, 24f), new Vector2(12f, 12f), AccentBlue);
            MakeRect(root, "GearToothB", center + new Vector2(0f, -24f), new Vector2(12f, 12f), AccentBlue);
            MakeRect(root, "GearToothC", center + new Vector2(24f, 0f), new Vector2(12f, 12f), AccentBlue);
            MakeRect(root, "GearToothD", center + new Vector2(-24f, 0f), new Vector2(12f, 12f), AccentBlue);
            MakeRect(root, "GearHole", center, new Vector2(12f, 12f), PanelLight);
        }

        private static void MakeSpeakerIcon(RectTransform root, Vector2 center)
        {
            MakeRect(root, "SpeakerBody", center + new Vector2(-14f, 0f), new Vector2(18f, 22f), PanelShadow);
            MakeRect(root, "SpeakerCone", center + new Vector2(4f, 0f), new Vector2(24f, 34f), FrameMid);
            MakeRect(root, "SoundWaveA", center + new Vector2(28f, 0f), new Vector2(7f, 32f), AccentBlue);
        }

        private static void MakeMutedIcon(RectTransform root, Vector2 center)
        {
            MakeSpeakerIcon(root, center + new Vector2(-4f, 0f));
            MakeRect(root, "MuteSlash", center + new Vector2(20f, 0f), new Vector2(10f, 44f), FrameDark);
        }

        private static void MakeMusicIcon(RectTransform root, Vector2 center)
        {
            MakeRect(root, "MusicStem", center + new Vector2(12f, 10f), new Vector2(8f, 42f), PanelShadow);
            MakeRect(root, "MusicFlag", center + new Vector2(25f, 26f), new Vector2(28f, 8f), PanelShadow);
            MakeRect(root, "MusicNote", center + new Vector2(-4f, -18f), new Vector2(30f, 22f), AccentBlue);
        }

        private static void MakeTextGlyph(RectTransform root, Vector2 center)
        {
            MakeRect(root, "TextAStem", center + new Vector2(-16f, 0f), new Vector2(8f, 40f), PanelShadow);
            MakeRect(root, "TextACross", center + new Vector2(-4f, 2f), new Vector2(28f, 8f), PanelShadow);
            MakeRect(root, "TextSmall", center + new Vector2(28f, -6f), new Vector2(20f, 28f), FrameMid);
        }

        private static void MakeFlower(RectTransform root, Vector2 center)
        {
            MakeRect(root, "FlowerCenter", center, new Vector2(14f, 14f), PanelShadow);
            MakeRect(root, "FlowerTop", center + new Vector2(0f, 16f), new Vector2(12f, 16f), PanelShadow);
            MakeRect(root, "FlowerBottom", center + new Vector2(0f, -16f), new Vector2(12f, 16f), PanelShadow);
            MakeRect(root, "FlowerLeft", center + new Vector2(-16f, 0f), new Vector2(16f, 12f), PanelShadow);
            MakeRect(root, "FlowerRight", center + new Vector2(16f, 0f), new Vector2(16f, 12f), PanelShadow);
        }

        private static void MakeCursor(RectTransform root, Vector2 center)
        {
            MakeRect(root, "CursorPalm", center + new Vector2(-10f, -2f), new Vector2(30f, 28f), Color.white);
            MakeRect(root, "CursorFinger", center + new Vector2(4f, 16f), new Vector2(14f, 38f), Color.white);
            MakeRect(root, "CursorOutline", center + new Vector2(8f, -20f), new Vector2(36f, 10f), FrameDark);
        }

        private static void MakePatternRow(RectTransform root, float y)
        {
            for (int i = 0; i < 17; i++)
            {
                float x = -230f + i * 28f;
                MakeRect(root, "PatternA" + y + "_" + i, new Vector2(x, y), new Vector2(8f, 8f), FrameMid);
                MakeRect(root, "PatternB" + y + "_" + i, new Vector2(x + 8f, y), new Vector2(5f, 5f), PanelShadow);
            }
        }

        private static void MakeCornerOrnaments(RectTransform root)
        {
            Vector2[] positions =
            {
                new Vector2(-300f, 202f),
                new Vector2(300f, 202f),
                new Vector2(-300f, -202f),
                new Vector2(300f, -202f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                MakeRect(root, "CornerOrnament" + i, positions[i], new Vector2(18f, 18f), AccentGold);
            }
        }

        private static Image MakeRect(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
