using System;
using System.Collections.Generic;
using Rootborn.Core.Settings;
using Rootborn.Ui;

namespace Rootborn.Screens.Settings
{
    /// <summary>
    /// 설정 화면의 표시 모델: 켬/끔 네 칸의 값. 값은 Core GameSettings에 있고(Unity SettingsPanel의 처음 모양: 소리 켬, 음소거 끔, 음악 끔,
    /// 글자 켬), 바꾸면 설정 파일에 저장한다. 칸 순서와 설정 이름은 Unity MakeSettingsGroup 순서(옆 아이콘 이름)다. 뜻은 아직 없다.
    /// </summary>
    public sealed class SettingsModel
    {
        /// <summary>줄마다 두 칸: [소리, 음소거], [음악, 글자]. Unity MakeSettingsGroup 순서.</summary>
        public static readonly string[] ToggleNames = { GameSettingNames.Speaker, GameSettingNames.Muted, GameSettingNames.Music, GameSettingNames.Text };
        public static int ToggleCount => ToggleNames.Length;

        private readonly SettingsStore _store;

        public SettingsModel(SettingsStore store) { _store = store ?? SettingsStore.InMemory(); }

        public bool IsOn(int index) => _store.Settings.GetToggle(ToggleNames[index]);

        public void Set(int index, bool on)
        {
            if (IsOn(index) == on) return;
            _store.Settings.SetToggle(ToggleNames[index], on);
            _store.Save();
        }
    }

    /// <summary>
    /// 설정(Esc). Unity SettingsPanel(660x510, 가운데)을 같은 수치로 옮겼다: 바탕·테두리, 위 탭 다섯, 양옆 꽃무늬 기둥, 장식 스크롤 막대,
    /// 켬/끔 네 칸 묶음, 아이콘 다섯 칸 묶음, 모서리 장식, 무늬 줄. Unity 판에는 글자가 없었다.
    /// 더한 것: 두 묶음 사이의 언어 줄과 표시 모드 줄(이름 + 지금 값 단추). 표시 모드 단추는 창 ↔ 테두리 없는 전체 화면을 바꾸고(F11과 같음)
    /// 바로 적용·저장한다. 언어 줄: 단추를 누르면 언어 파일 폴더에 있는 언어를 사전 순으로 돌고, 열린 화면 글자가
    /// 바로 바뀌며 설정 파일에 저장된다. 언어 이름은 그 언어 파일의 ui.settings.language-name이다(언어를 더하는 일은 파일을 더하는 일).
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

        /// <summary>
        /// 언어 줄·표시 모드 줄 배치(패널 가운데 기준, Unity 좌표처럼 y가 위로). 켬/끔 묶음(아래 끝 -10)과 아이콘 묶음(위 끝 IconGroupTop) 사이에
        /// 두 줄을 둔다. 두 줄이 들어가도록 아이콘 묶음(Unity 판에서 뜻 없는 장식)을 아래로 내리고 낮췄다.
        /// </summary>
        internal static class OptionRows
        {
            public const float LanguageY = -32f, DisplayY = -80f, Height = 40f, Font = 18f, LabelFont = 24f;
            public const float LabelX = -98f, LabelW = 170f;
            public const float ButtonX = 92f, ButtonW = 190f;
            public const float IconGroupY = -150f, IconGroupH = 84f;
        }

        public const string LanguageLabelId = "settings.language.label";
        public const string LanguageButtonId = "settings.language.value";
        public const string DisplayLabelId = "settings.display.label";
        public const string DisplayButtonId = "settings.display.value";

        private readonly UiToggle[] _toggles = new UiToggle[SettingsModel.ToggleNames.Length];
        private UiLabel _languageLabel;
        private UiButton _languageButton;
        private UiLabel _displayLabel;
        private UiButton _displayButton;

        public SettingsScreen(ScreenServices services) : base(services)
        {
            Model = new SettingsModel(services.Settings);
        }

        public SettingsModel Model { get; }

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
            LanguageLine(root);
            DisplayLine(root);
            IconGroup(root, 0f, OptionRows.IconGroupY);
            CornerOrnaments(root);
            PatternRow(root, 154f);
            PatternRow(root, -204f);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            for (int i = 0; i < _toggles.Length; i++) _toggles[i].IsOn = Model.IsOn(i);
            WriteLanguage();
            WriteDisplay();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) { WriteLanguage(); WriteDisplay(); }
            else if (topic == ChangeTopics.Display) WriteDisplay();
        }

        private void LanguageLine(UiNode root)
        {
            _languageLabel = root.Add(new UiLabel { Id = LanguageLabelId, FontSize = OptionRows.LabelFont, Color = UiTheme.Ink, Align = UiAlign.Right })
                .Centered(OptionRows.LabelX, OptionRows.LanguageY, OptionRows.LabelW, OptionRows.Height);
            _languageButton = root.Add(new UiButton { Id = LanguageButtonId, FontSize = OptionRows.Font })
                .Centered(OptionRows.ButtonX, OptionRows.LanguageY, OptionRows.ButtonW, OptionRows.Height);
            _languageButton.Clicked += NextLanguage;
        }

        private void DisplayLine(UiNode root)
        {
            _displayLabel = root.Add(new UiLabel { Id = DisplayLabelId, FontSize = OptionRows.LabelFont, Color = UiTheme.Ink, Align = UiAlign.Right })
                .Centered(OptionRows.LabelX, OptionRows.DisplayY, OptionRows.LabelW, OptionRows.Height);
            _displayButton = root.Add(new UiButton { Id = DisplayButtonId, FontSize = OptionRows.Font })
                .Centered(OptionRows.ButtonX, OptionRows.DisplayY, OptionRows.ButtonW, OptionRows.Height);
            _displayButton.Clicked += FlipDisplay;
        }

        /// <summary>표시 모드 단추: 지금 모드 이름. Host 창이 없으면(창 없이 도는 화면) 누를 수 없다.</summary>
        private void WriteDisplay()
        {
            DisplayModeSetting display = Services.Display;
            _displayLabel.Text = T("ui.settings.display");
            _displayButton.Text = display != null && display.Fullscreen ? T("ui.settings.display-fullscreen") : T("ui.settings.display-windowed");
            _displayButton.Enabled = display != null;
        }

        /// <summary>창 ↔ 테두리 없는 전체 화면. 저장과 창 적용은 DisplayModeSetting과 Host가 한다. 글자는 Display 알림으로 바뀐다.</summary>
        private void FlipDisplay()
        {
            DisplayModeSetting display = Services.Display;
            if (display != null) display.Fullscreen = !display.Fullscreen;
        }

        private void WriteLanguage()
        {
            _languageLabel.Text = T("ui.settings.language");
            _languageButton.Text = T("ui.settings.language-name");
        }

        /// <summary>언어 파일이 있는 언어를 사전 순으로 하나 넘긴다. 글자는 GameText.LanguageChanged 알림으로 바뀐다.</summary>
        private void NextLanguage()
        {
            var languages = new List<string>(Services.Text.Languages);
            if (languages.Count < 2) return;
            languages.Sort(StringComparer.OrdinalIgnoreCase);
            int at = languages.FindIndex(l => string.Equals(l, Services.Text.Language, StringComparison.OrdinalIgnoreCase));
            string next = languages[(at + 1) % languages.Count];
            Services.Settings.Settings.Language = next;
            Services.Settings.Save();
            Services.Text.Language = next;
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
            float h = OptionRows.IconGroupH;
            Box(root, cx, cy, 386f, h, FrameDark);
            Box(root, cx, cy, 356f, h - 22f, PanelLight);
            Box(root, cx, cy, 328f, h - 36f, PanelFill);
            for (int i = 0; i < 5; i++)
            {
                float x = cx - 128f + i * 64f;
                Box(root, x, cy, 42f, 42f, FrameMid);
                Box(root, x, cy, 28f, 28f, i == 0 ? White : AccentBlue);
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
