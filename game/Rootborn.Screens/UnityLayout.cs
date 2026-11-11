using System;
using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>
    /// Unity 판 화면 수치를 옮길 때 쓰는 배치 도움. Unity RectTransform(가운데 앵커·가운데 기준점, y는 위로 커짐)의
    /// anchoredPosition·sizeDelta를 그대로 받아 UiNode 배치(왼쪽 위 기준, y는 아래로 커짐)로 바꾼다.
    /// </summary>
    public static class UnityLayout
    {
        /// <summary>부모 가운데에서 (x, y)만큼 떨어진 곳에 가운데를 둔 w x h.</summary>
        public static T Centered<T>(this T node, float x, float y, float w, float h) where T : UiNode
        {
            node.Place(0.5f, 0.5f, x - w / 2f, -y - h / 2f, w, h);
            return node;
        }

        /// <summary>Unity Color(0~1 실수)를 바이트 색으로(Unity Color32 변환과 같은 반올림).</summary>
        public static UiColor Color(float r, float g, float b, float a = 1f) =>
            new UiColor(Byte(r), Byte(g), Byte(b), Byte(a));

        private static byte Byte(float v) => (byte)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);
    }
}
