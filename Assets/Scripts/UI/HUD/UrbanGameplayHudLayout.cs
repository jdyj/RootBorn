using UnityEngine;
namespace Rootborn.UI.HUD
{
    public static class UrbanGameplayHudLayout
    {
        private const float EdgeControls = 204f;
        private const float Gap = 16f;
        private const float Top = 36f;
        private const float ResourceWidth = 140f;
        private const float RowHeight = 144f;
        private const float ClockWidth = 420f;

        public static void PositionClock(RectTransform rect)
        {
            PositionFromRight(rect, EdgeControls + ResourceWidth * 2 + Gap * 2 + ClockWidth, Top, new Vector2(ClockWidth, RowHeight));
        }

        public static void PositionResource(RectTransform rect, bool focus)
        {
            float inset = EdgeControls + ResourceWidth + (focus ? 0f : ResourceWidth + Gap);
            PositionFromRight(rect, inset, Top, new Vector2(ResourceWidth, RowHeight));
        }

        public static void PositionTracked(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-EdgeControls, -(Top + RowHeight + Gap));
            rect.sizeDelta = new Vector2(Mathf.Max(640f, rect.sizeDelta.x), Mathf.Max(200f, rect.sizeDelta.y));
        }

        private static void PositionFromRight(RectTransform rect, float inset, float top, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(-inset, -top);
            rect.sizeDelta = size;
        }
    }
}
