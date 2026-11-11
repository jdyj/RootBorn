using System;
using Rootborn.Ui;

namespace Rootborn.Screens.Settings
{
    /// <summary>
    /// 설정 화면의 표시 모델: 켬/끔 네 칸의 값. Unity SettingsPanel의 처음 모양(소리 켬, 음소거 끔, 음악 끔, 글자 켬)에서 시작한다.
    /// Unity 판은 이 값을 어디에도 저장하지 않았고 켬/끔도 눌리지 않는 그림이었다. 여기서는 눌러서 바뀌지만 저장하지 않는다
    /// (저장할 설정 영역이 Core·Game에 없다 — 보고에 적음).
    /// </summary>
    public sealed class SettingsModel
    {
        /// <summary>줄마다 두 칸: [소리, 음소거], [음악, 글자]. Unity MakeSettingsGroup 순서.</summary>
        public const int ToggleCount = 4;

        private readonly bool[] _on = { true, false, false, true };

        public bool IsOn(int index) => _on[index];

        public void Set(int index, bool on) => _on[index] = on;
    }

    /// <summary>
    /// 설정(Esc). Unity SettingsPanel(660x510, 가운데)을 같은 수치로 옮겼다: 바탕·테두리, 위 탭 다섯, 양옆 꽃무늬 기둥, 장식 스크롤 막대,
    /// 켬/끔 네 칸 묶음, 아이콘 다섯 칸 묶음, 모서리 장식, 무늬 줄. Unity 판에는 글자가 없다(문구 키 없음).
    /// 이동을 막지 않는다. 닫기는 Esc(키 배정 표의 열고 닫기).
    /// </summary>
    public sealed class SettingsScreen : GameScreen
    {
        public const float Width = 660f, Height = 510f;

        private static readonly UiColor FrameDark = UnityLayout.Color(0.38f, 0.26f, 0.24f);
        private static readonly UiColor FrameMid = UnityLayout.Color(0.59f, 0.45f, 0.37f);
        private static readonly UiColor PanelFill = UnityLayout.Color(0.75f, 0.65f, 0.52f);
        private static readonly UiColor PanelLight = UnityLayout.Color(0.87f, 0.76f, 0.62f);
        private static readonly UiColor PanelShadow = UnityLayout.Color(0.44f, 0.33f, 0.30f);
        private static readonly UiColor AccentBlue = UnityLayout.Color(0.37f, 0.37f, 0.55f);
        private static readonly UiColor AccentGreen = UnityLayout.Color(0.18f, 0.55f, 0.34f);
        private static readonly UiColor AccentRed = UnityLayout.Color(0.77f, 0.20f, 0.18f);
        private static readonly UiColor AccentGold = UnityLayout.Color(0.84f, 0.57f, 0.28f);
        private static readonly UiColor White = UnityLayout.Color(1f, 1f, 1f);

        private readonly UiToggle[] _toggles = new UiToggle[SettingsModel.ToggleCount];

        public SettingsScreen(ScreenServices services) : base(services) { }

        public SettingsModel Model { get; } = new SettingsModel();

        public static string ToggleId(int index) => "settings.toggle." + index;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = "settings", Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Width, Height);

            Box(root, 0f, 0f, 660f, 510f, PanelFill);
            Box(root, 0f, 230f, 664f, 16f, FrameDark);
            Box(root, 0f, -230f, 664f, 16f, FrameDark);
            Box(root, -326f, 0f, 16f, 456f, FrameDark);
            Box(root, 326f, 0f, 16f, 456f, FrameDark);
            Box(root, 0f, 0f, 604f, 390f, PanelLight);
            Box(root, 0f, -6f, 574f, 348f, PanelFill);

            TopTabs(root);
            SideRail(root, -252f);
            SideRail(root, 252f);
            Scrollbar(root);
            SettingsGroup(root, 0f, 58f);
            IconGroup(root, 0f, -126f);
            CornerOrnaments(root);
            PatternRow(root, 154f);
            PatternRow(root, -204f);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            for (int i = 0; i < _toggles.Length; i++) _toggles[i].IsOn = Model.IsOn(i);
        }

        private static UiBox Box(UiNode parent, float x, float y, float w, float h, UiColor color) =>
            parent.Add(new UiBox { Color = color }).Centered(x, y, w, h);

        private static void TopTabs(UiNode root)
        {
            float[] xs = { -228f, -112f, 0f, 112f, 228f };
            UiColor[] colors = { AccentGold, AccentGreen, AccentRed, AccentBlue, PanelLight };
            for (int i = 0; i < xs.Length; i++)
            {
                Box(root, xs[i], 220f, 92f, 70f, FrameDark);
                Box(root, xs[i], 218f, 72f, 50f, PanelFill);
                Box(root, xs[i], 218f, 38f, 30f, colors[i]);
            }
            Gear(root, 228f, 218f);
        }

        private void SettingsGroup(UiNode root, float cx, float cy)
        {
            Box(root, cx, cy, 386f, 136f, FrameDark);
            Box(root, cx, cy, 356f, 102f, PanelLight);
            Box(root, cx, cy, 328f, 82f, PanelFill);

            Speaker(root, cx - 132f, cy + 26f);
            Toggle(root, 0, cx - 38f, cy + 25f);
            Muted(root, cx + 42f, cy + 26f);
            Toggle(root, 1, cx + 132f, cy + 25f);
            Music(root, cx - 132f, cy - 32f);
            Toggle(root, 2, cx - 38f, cy - 32f);
            TextGlyph(root, cx + 42f, cy - 32f);
            Toggle(root, 3, cx + 132f, cy - 32f);
        }

        private void Toggle(UiNode root, int index, float x, float y)
        {
            var toggle = root.Add(new UiToggle { Id = ToggleId(index), IsOn = Model.IsOn(index) }).Centered(x, y, 78f, 36f);
            toggle.Toggled += on => Model.Set(index, on);
            _toggles[index] = toggle;
        }

        private static void IconGroup(UiNode root, float cx, float cy)
        {
            Box(root, cx, cy, 386f, 116f, FrameDark);
            Box(root, cx, cy, 356f, 82f, PanelLight);
            Box(root, cx, cy, 328f, 62f, PanelFill);
            for (int i = 0; i < 5; i++)
            {
                float x = cx - 128f + i * 64f;
                Box(root, x, cy, 50f, 50f, FrameMid);
                Box(root, x, cy, 34f, 34f, i == 0 ? White : AccentBlue);
            }
            Cursor(root, cx - 128f, cy - 2f);
        }

        private static void SideRail(UiNode root, float x)
        {
            Box(root, x, -22f, 62f, 292f, FrameMid);
            Box(root, x, -22f, 38f, 250f, PanelFill);
            for (int i = 0; i < 5; i++) Flower(root, x, 82f - i * 48f);
        }

        private static void Scrollbar(UiNode root)
        {
            Box(root, 294f, -22f, 18f, 292f, FrameDark);
            Box(root, 294f, -22f, 8f, 244f, PanelShadow);
            Box(root, 294f, 118f, 32f, 24f, FrameMid);
        }

        private static void Gear(UiNode root, float x, float y)
        {
            Box(root, x, y, 30f, 30f, AccentBlue);
            Box(root, x, y + 24f, 12f, 12f, AccentBlue);
            Box(root, x, y - 24f, 12f, 12f, AccentBlue);
            Box(root, x + 24f, y, 12f, 12f, AccentBlue);
            Box(root, x - 24f, y, 12f, 12f, AccentBlue);
            Box(root, x, y, 12f, 12f, PanelLight);
        }

        private static void Speaker(UiNode root, float x, float y)
        {
            Box(root, x - 14f, y, 18f, 22f, PanelShadow);
            Box(root, x + 4f, y, 24f, 34f, FrameMid);
            Box(root, x + 28f, y, 7f, 32f, AccentBlue);
        }

        private static void Muted(UiNode root, float x, float y)
        {
            Speaker(root, x - 4f, y);
            Box(root, x + 20f, y, 10f, 44f, FrameDark);
        }

        private static void Music(UiNode root, float x, float y)
        {
            Box(root, x + 12f, y + 10f, 8f, 42f, PanelShadow);
            Box(root, x + 25f, y + 26f, 28f, 8f, PanelShadow);
            Box(root, x - 4f, y - 18f, 30f, 22f, AccentBlue);
        }

        private static void TextGlyph(UiNode root, float x, float y)
        {
            Box(root, x - 16f, y, 8f, 40f, PanelShadow);
            Box(root, x - 4f, y + 2f, 28f, 8f, PanelShadow);
            Box(root, x + 28f, y - 6f, 20f, 28f, FrameMid);
        }

        private static void Flower(UiNode root, float x, float y)
        {
            Box(root, x, y, 14f, 14f, PanelShadow);
            Box(root, x, y + 16f, 12f, 16f, PanelShadow);
            Box(root, x, y - 16f, 12f, 16f, PanelShadow);
            Box(root, x - 16f, y, 16f, 12f, PanelShadow);
            Box(root, x + 16f, y, 16f, 12f, PanelShadow);
        }

        private static void Cursor(UiNode root, float x, float y)
        {
            Box(root, x - 10f, y - 2f, 30f, 28f, White);
            Box(root, x + 4f, y + 16f, 14f, 38f, White);
            Box(root, x + 8f, y - 20f, 36f, 10f, FrameDark);
        }

        private static void CornerOrnaments(UiNode root)
        {
            Box(root, -300f, 202f, 18f, 18f, AccentGold);
            Box(root, 300f, 202f, 18f, 18f, AccentGold);
            Box(root, -300f, -202f, 18f, 18f, AccentGold);
            Box(root, 300f, -202f, 18f, 18f, AccentGold);
        }

        private static void PatternRow(UiNode root, float y)
        {
            for (int i = 0; i < 17; i++)
            {
                float x = -230f + i * 28f;
                Box(root, x, y, 8f, 8f, FrameMid);
                Box(root, x + 8f, y, 5f, 5f, PanelShadow);
            }
        }
    }
}
