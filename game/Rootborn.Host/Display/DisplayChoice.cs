using System;

namespace Rootborn.Host.Display
{
    /// <summary>
    /// 창 모드·전체 화면과 창 크기를 정하는 순수 계산(MonoGame 없이 테스트한다).
    /// Unity 판(ProjectSettings fullscreenMode 3, resizableWindow, 기본 1920x1080, 최소 1280x720)과 같은 기준이다.
    /// </summary>
    public static class DisplayChoice
    {
        /// <summary>설정 파일(Core GameSettings 켬/끔 목록)에 표시 모드를 적는 이름. 켬 = 테두리 없는 전체 화면.</summary>
        public const string FullscreenSetting = "Fullscreen";

        /// <summary>Unity 판 기본값: 테두리 없는 전체 화면(fullscreenMode 3).</summary>
        public const bool DefaultFullscreen = true;

        /// <summary>창 모드에서 줄일 수 있는 가장 작은 크기(Unity 판 최소 지원 해상도).</summary>
        public const int MinimumWidth = 1280, MinimumHeight = 720;

        /// <summary>
        /// 처음 시작할 때 전체 화면인가. 테스트 모드는 늘 창 모드(테스트가 사용자의 화면을 덮지 않는다).
        /// 그다음 실행 인자(--fullscreen/--windowed), --size를 준 실행은 창 모드, 저장된 설정, 기본값 순이다.
        /// </summary>
        public static bool StartFullscreen(bool? requested, bool sizeGiven, bool testMode, bool? saved)
        {
            if (testMode) return false;
            if (requested.HasValue) return requested.Value;
            if (sizeGiven) return false;
            if (saved.HasValue) return saved.Value;
            return DefaultFullscreen;
        }

        /// <summary>
        /// 창의 최소 크기. 1280x720이되, 실행 인자로 그보다 작은 창을 달라고 했으면(시험용 작은 창) 그 크기까지 허용한다.
        /// </summary>
        public static (int Width, int Height) MinimumSize(int startWidth, int startHeight) =>
            (Math.Min(MinimumWidth, startWidth), Math.Min(MinimumHeight, startHeight));

        /// <summary>창 크기를 최소 크기 아래로 내리지 않는다(최소보다 작게 줄이면 최소 크기에서 멈춘다).</summary>
        public static (int Width, int Height) ClampToMinimum(int width, int height, (int Width, int Height) minimum) =>
            (Math.Max(width, minimum.Width), Math.Max(height, minimum.Height));
    }
}
