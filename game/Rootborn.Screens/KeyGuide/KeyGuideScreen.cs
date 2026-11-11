using System;
using System.Collections.Generic;
using Rootborn.Ui;
using M = Rootborn.Screens.KeyGuide.KeyGuideScreen.Metrics;

namespace Rootborn.Screens.KeyGuide
{
    /// <summary>
    /// 안내 목록의 한 줄(묶음 제목 또는 키 줄)이 그릴 글자와, 줄 폭에 맞춘 자리(줄 왼쪽 위 기준). 자리는 글자나 폭이 바뀔 때만 다시 잰다
    /// (매 프레임 새 글자를 만들지 않는다). 키 모양은 오른쪽으로 붙고, 하는 일 글자는 남은 폭에 맞춰 말줄임한다.
    /// </summary>
    public sealed class KeyGuideLine
    {
        internal KeyGuideLine(string header) { Header = header; Label = header; }

        internal KeyGuideLine(string label, string[][] chords) { Label = label; Chords = chords; }

        /// <summary>묶음 제목이면 그 글자, 키 줄이면 null.</summary>
        public string Header { get; }
        public bool IsHeader => Header != null;
        /// <summary>하는 일 글자(묶음 제목이면 제목).</summary>
        public string Label { get; }
        /// <summary>같은 일을 하는 키 조합들, 조합마다 키 모양 글자들.</summary>
        public string[][] Chords { get; }

        public string FittedLabel { get; private set; }
        public UiRect LabelRect { get; private set; }
        /// <summary>키 모양 사각형(조합 순서, 조합 안 키 순서로 이어 붙임).</summary>
        public UiRect[] CapRects { get; private set; } = Array.Empty<UiRect>();
        public string[] CapTexts { get; private set; } = Array.Empty<string>();
        /// <summary>키 사이 기호("+" 함께 누름, "/" 어느 것이든)와 그 자리.</summary>
        public string[] Separators { get; private set; } = Array.Empty<string>();
        public UiRect[] SeparatorRects { get; private set; } = Array.Empty<UiRect>();

        private float _laidWidth = -1f;
        private ITextMeasurer _laidWith;

        public void Layout(float width, ITextMeasurer text)
        {
            if (_laidWidth == width && ReferenceEquals(_laidWith, text)) return;
            _laidWidth = width; _laidWith = text;
            if (IsHeader)
            {
                LabelRect = new UiRect(M.TextInset, 0f, Math.Max(0f, width - M.TextInset * 2f), M.RowH);
                FittedLabel = TextFit.Ellipsize(Header, LabelRect.W, text, M.HeaderFont);
                return;
            }

            int capCount = 0;
            foreach (string[] chord in Chords) capCount += chord.Length;
            var caps = new UiRect[capCount];
            var capTexts = new string[capCount];
            var seps = new List<string>(capCount);
            var sepRects = new List<UiRect>(capCount);

            // 오른쪽 끝에서 왼쪽으로 쌓는다.
            float capTop = (M.RowH - M.CapH) / 2f;
            float right = width - M.TextInset;
            int at = capCount - 1;
            for (int a = Chords.Length - 1; a >= 0; a--)
            {
                string[] chord = Chords[a];
                for (int k = chord.Length - 1; k >= 0; k--)
                {
                    string shown = TextFit.Ellipsize(chord[k], M.CapMaxW - M.CapPadX * 2f, text, M.CapFont);
                    float w = Math.Max(M.CapMinW, text.Width(shown, M.CapFont) + M.CapPadX * 2f);
                    caps[at] = new UiRect(right - w, capTop, w, M.CapH);
                    capTexts[at] = shown;
                    at--;
                    right -= w;
                    if (k > 0) right = AddSeparator("+", right, text, seps, sepRects);
                }
                if (a > 0) right = AddSeparator("/", right, text, seps, sepRects);
            }
            CapRects = caps;
            CapTexts = capTexts;
            Separators = seps.ToArray();
            SeparatorRects = sepRects.ToArray();

            float labelW = Math.Max(0f, right - M.LabelGap - M.TextInset);
            LabelRect = new UiRect(M.TextInset, 0f, labelW, M.RowH);
            FittedLabel = TextFit.Ellipsize(Label, labelW, text, M.LabelFont);
        }

        private static float AddSeparator(string symbol, float right, ITextMeasurer text, List<string> seps, List<UiRect> rects)
        {
            float w = text.Width(symbol, M.CapFont) + M.SeparatorPad * 2f;
            rects.Add(new UiRect(right - w, 0f, w, M.RowH));
            seps.Add(symbol);
            return right - w;
        }
    }

    /// <summary>
    /// 단축키 안내(F1, 설정 화면의 "단축키" 단추, 시작 화면에서도 F1). 가운데 모달 패널: 제목, 닫기 단추, 묶음별 줄 목록
    /// (보이는 줄만 그린다, 넘치면 스크롤). 줄은 KeyGuideModel이 등록 표·화면 쌓기·Host 이름표에서 만든다 — 여기에 키 목록을 적지 않는다.
    /// 키 모양은 스킨 이름(slot)으로 그린다. 이동을 막는다(읽는 화면). Esc·F1·닫기 단추로 닫는다. Unity 판에는 없던 화면이다.
    /// </summary>
    public sealed class KeyGuideScreen : GameScreen
    {
        /// <summary>배치 — 새 디자인에서는 여기만 고친다. 패널은 가운데 기준, 안쪽은 패널 왼쪽 위 기준.</summary>
        public static class Metrics
        {
            public const float PanelW = 1200f, PanelH = 960f;
            public const float TitleTop = 24f, TitleH = 52f, TitleFont = 30f, TitleInset = 48f;
            public const float CloseSize = 56f, CloseIconSize = 32f, CloseInset = 24f;
            public const float ListX = 48f, ListTop = 96f, ListBottom = 28f;
            public const float RowH = 40f, RowGap = 2f;
            public const float TextInset = 12f, LabelGap = 24f;
            public const float HeaderFont = 24f, LabelFont = 20f;
            public const float CapH = 32f, CapFont = 18f, CapPadX = 12f, CapMinW = 44f, CapMaxW = 260f, SeparatorPad = 6f;
            public static readonly UiColor RowColor = UnityLayout.Color(1f, 1f, 1f, 0.18f);
            public static readonly UiColor HeaderColor = UnityLayout.Color(0f, 0f, 0f, 0.08f);
        }

        public const string PanelId = "key-guide";
        public const string TitleId = "key-guide.title";
        public const string CloseId = "key-guide.close";
        public const string ListId = "key-guide.rows";

        private UiLabel _title;
        private UiCardGrid _grid;
        private readonly List<KeyGuideLine> _lines = new List<KeyGuideLine>(48);

        public KeyGuideScreen(ScreenServices services) : base(services) { }

        public KeyGuideModel Model { get; private set; }

        /// <summary>지금 보이는 줄(묶음 제목 포함, 위에서 아래로).</summary>
        public IReadOnlyList<KeyGuideLine> Lines => _lines;

        public UiCardGrid List => _grid;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "key-guide.root" };
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            panel.Centered(0f, 0f, Metrics.PanelW, Metrics.PanelH);

            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Align = UiAlign.Center, Color = UiTheme.Ink });
            _title.Place(0f, 0f, Metrics.TitleInset + Metrics.CloseSize, Metrics.TitleTop, Metrics.PanelW - (Metrics.TitleInset + Metrics.CloseSize) * 2f, Metrics.TitleH);

            var close = panel.Add(new UiButton { Id = CloseId, Skin = UiSkin.Slot });
            close.Place(1f, 0f, -Metrics.CloseInset - Metrics.CloseSize, Metrics.CloseInset, Metrics.CloseSize, Metrics.CloseSize);
            close.Clicked += Close;
            float inset = (Metrics.CloseSize - Metrics.CloseIconSize) / 2f;
            close.Add(new UiImage { Image = "icon:close" }).Place(0f, 0f, inset, inset, Metrics.CloseIconSize, Metrics.CloseIconSize);

            float listW = Metrics.PanelW - Metrics.ListX * 2f;
            _grid = panel.Add(new UiCardGrid
            {
                Id = ListId, Padding = 0f, DrawBackground = false, Spacing = Metrics.RowGap, StepsPerNotch = 2f,
                CellWidth = listW - UiCardScroller.ScrollbarWidth - UiCardScroller.ScrollbarGap, CellHeight = Metrics.RowH,
            });
            _grid.Place(0f, 0f, Metrics.ListX, Metrics.ListTop, listW, Metrics.PanelH - Metrics.ListTop - Metrics.ListBottom);
            _grid.DrawCell = DrawLine;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape && key != ScreenKey.Help) return false;
            Close();
            return true;
        }

        private void Refresh()
        {
            _ = Root;
            _title.Text = T("ui.keys.title");
            ScreenRegistry registry = Services.Screens?.Registry ?? ScreenCatalog.Create();
            Model = KeyGuideModel.From(registry, Services.KeyGuide);
            _lines.Clear();
            string group = null;
            foreach (KeyGuideRow row in Model.Rows)
            {
                if (row.Group != group)
                {
                    group = row.Group;
                    _lines.Add(new KeyGuideLine(T(KeyGuideGroups.TitleKey(group))));
                }
                var chords = new string[row.Alternatives.Count][];
                for (int a = 0; a < chords.Length; a++)
                {
                    IReadOnlyList<KeyCap> caps = row.Alternatives[a].Caps;
                    chords[a] = new string[caps.Count];
                    for (int k = 0; k < caps.Count; k++) chords[a][k] = caps[k].Localized ? T(caps[k].Text) : caps[k].Text;
                }
                _lines.Add(new KeyGuideLine(T(row.ActionKey), chords));
            }
            _grid.ItemCount = _lines.Count;
            _grid.ScrollOffset = 0f;
        }

        private void DrawLine(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (index >= _lines.Count) return;
            KeyGuideLine line = _lines[index];
            line.Layout(rect.W, text);
            if (line.IsHeader)
            {
                list.Fill(rect, Metrics.HeaderColor);
                list.Text(rect.X + line.LabelRect.X, rect.Y + (Metrics.RowH - text.LineHeight(Metrics.HeaderFont)) / 2f, line.FittedLabel, Metrics.HeaderFont, UiTheme.Ink);
                return;
            }
            list.Fill(rect, Metrics.RowColor);
            list.Text(rect.X + line.LabelRect.X, rect.Y + (Metrics.RowH - text.LineHeight(Metrics.LabelFont)) / 2f, line.FittedLabel, Metrics.LabelFont, UiTheme.Ink);
            float capTextY = (Metrics.CapH - text.LineHeight(Metrics.CapFont)) / 2f;
            for (int i = 0; i < line.CapRects.Length; i++)
            {
                UiRect cap = line.CapRects[i];
                var at = new UiRect(rect.X + cap.X, rect.Y + cap.Y, cap.W, cap.H);
                list.Panel(at, UiSkin.Slot, UiTheme.White);
                string shown = line.CapTexts[i];
                list.Text(at.X + (at.W - text.Width(shown, Metrics.CapFont)) / 2f, at.Y + capTextY, shown, Metrics.CapFont, UiTheme.Ink, UiSkin.Slot);
            }
            float sepY = (Metrics.RowH - text.LineHeight(Metrics.CapFont)) / 2f;
            for (int i = 0; i < line.Separators.Length; i++)
            {
                UiRect sep = line.SeparatorRects[i];
                list.Text(rect.X + sep.X + Metrics.SeparatorPad, rect.Y + sepY, line.Separators[i], Metrics.CapFont, UiTheme.Ink);
            }
            if (state.Selected && state.Focused) list.Outline(rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
