using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 화면이 차지하는 곳: 보이는 요소 중 가장 바깥 겉면(IsSurface). 겉면 안의 요소는 그 겉면이 대신한다.
    /// 화면끼리 겹치는지 보고(화면 쌓기, 테스트) 위층이 덮은 아래층 요소를 정하는 데 쓴다.
    /// </summary>
    public static class UiSurfaces
    {
        /// <param name="includeCovered">덮인 요소도 모은다(덮임을 다시 정할 때).</param>
        public static void Collect(UiNode root, List<UiNode> into, bool includeCovered = false)
        {
            if (root == null || !root.Visible || (!includeCovered && root.Covered)) return;
            if (root.IsSurface && root.Rect.W > 0 && root.Rect.H > 0) { into.Add(root); return; }
            for (int i = 0; i < root.Children.Count; i++) Collect(root.Children[i], into, includeCovered);
        }

        /// <summary>두 사각형이 반 픽셀 넘게 겹친다(맞닿기만 하면 겹치지 않는다).</summary>
        public static bool Overlap(UiRect a, UiRect b)
            => a.X < b.Right - 0.5f && b.X < a.Right - 0.5f && a.Y < b.Bottom - 0.5f && b.Y < a.Bottom - 0.5f;
    }
}
