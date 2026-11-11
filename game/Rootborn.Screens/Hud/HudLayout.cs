using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 플레이 화면 위쪽 줄의 배치(1920x1080 기준). Unity UrbanGameplayHudLayout의 수치다: 오른쪽 끝에서 버튼 자리(204)를 비우고
    /// 오른쪽부터 집중·체력 칸(140), 그 왼쪽에 시계(420), 줄 높이 144, 위 여백 36, 칸 사이 16. 추적 목표는 그 줄 아래 오른쪽(640x200 이상).
    /// 새 디자인에서 위쪽 줄을 바꾸려면 여기만 고친다.
    /// </summary>
    public static class HudLayout
    {
        public const float EdgeControls = 204f;
        public const float Gap = 16f;
        public const float Top = 36f;
        public const float ResourceWidth = 140f;
        public const float RowHeight = 144f;
        public const float ClockWidth = 420f;
        public const float TrackedWidth = 640f;
        public const float TrackedHeight = 200f;

        /// <summary>HUD 글자 색(Unity Color32(48, 71, 83)).</summary>
        public static readonly UiColor Ink = new UiColor(48, 71, 83);

        /// <summary>시계: 오른쪽 끝에서 (204 + 140×2 + 16×2 + 420) 들어온 곳.</summary>
        public static T PlaceClock<T>(T node) where T : UiNode => FromRight(node, EdgeControls + ResourceWidth * 2 + Gap * 2 + ClockWidth, ClockWidth);

        /// <summary>생활 수치 칸: 집중이 오른쪽, 체력이 그 왼쪽.</summary>
        public static T PlaceResource<T>(T node, bool focus) where T : UiNode =>
            FromRight(node, EdgeControls + ResourceWidth + (focus ? 0f : ResourceWidth + Gap), ResourceWidth);

        /// <summary>추적 목표: 오른쪽 위 기준, 오른쪽 끝에서 204, 위쪽 줄 아래 16.</summary>
        public static T PlaceTracked<T>(T node) where T : UiNode
        {
            node.Place(1f, 0f, -EdgeControls - TrackedWidth, Top + RowHeight + Gap, TrackedWidth, TrackedHeight);
            return node;
        }

        private static T FromRight<T>(T node, float inset, float width) where T : UiNode
        {
            node.Place(1f, 0f, -inset, Top, width, RowHeight);
            return node;
        }
    }
}
