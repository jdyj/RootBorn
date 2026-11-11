using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>
    /// 진로 후보 화면의 표시 모델(Unity CareerCandidatePanel + CareerCandidateSummaryBuilder): 정의 순서의 후보 요약들.
    /// 잠긴 후보는 이름 대신 예고 문구(DisplayName)를 보인다 — 그 판단은 Core 요약 빌더가 한다.
    /// </summary>
    public sealed class CareerCandidateModel
    {
        public static readonly CareerCandidateModel Empty = new CareerCandidateModel(Array.Empty<CareerCandidateSummary>());

        public CareerCandidateModel(CareerCandidateSummary[] rows) { Rows = rows; }

        public CareerCandidateSummary[] Rows { get; }

        public static CareerCandidateModel From(GameSession session) =>
            session == null ? Empty : new CareerCandidateModel(CareerCandidateSummaryBuilder.BuildAll(session.Definitions.All<CareerCandidate>(), session.CareerCandidates, session.Student));

        /// <summary>후보 상태 문구 키. 상태 하나에 키 하나(새 상태는 문구표에 키 하나로 더한다).</summary>
        public static string StateKey(CareerCandidateState state) => "ui.career-candidate.state." + state.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// 진로 후보(Unity CareerCandidatePanel). HUD 버튼(ui.career-candidate.open)이 연다. 가운데 900x560 패널: 제목, 후보 개수, 왼쪽 후보 목록
    /// (이름과 상태 두 줄 버튼, 길면 스크롤), 오른쪽 상세(상태, 단서 수·이해도, 설명, 관련 장소·활동, 다음에 해볼 일), 닫기.
    /// 후보를 누르면 상세만 바뀐다(상태를 바꾸지 않는다). 이동을 막는다(Unity: UiGameplayInputLock). 열 때는 첫 후보의 상세를 보인다.
    /// </summary>
    public sealed class CareerCandidateScreen : CareerScreenBase
    {
        /// <summary>배치 — Unity CareerCandidatePanel.BuildIfNeeded 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 900f, Height = 560f;
            public const float TitleY = 238f, TitleW = 760f, TitleH = 38f, TitleFont = 26f;
            public const float CountX = -285f, CountY = 208f, CountW = 250f, CountH = 30f, CountFont = 18f;
            public const float ListX = -285f, ListY = -6f, ListW = 250f, ListH = 360f;
            public const float RowW = 250f, RowH = 52f, RowSpacing = 8f, RowFont = 16f;
            public const float DetailsX = 165f, DetailsY = 10f, DetailsW = 470f, DetailsH = 420f, DetailsFont = 18f;
            public const float CloseX = 370f, CloseY = -238f, CloseW = 120f, CloseH = 52f, CloseFont = 16f;
            public static readonly UiColor Ink = UnityLayout.Color(0.25f, 0.18f, 0.12f);
        }

        public const string PanelId = "career-candidates";
        public const string TitleId = "career-candidates.title";
        public const string CountId = "career-candidates.count";
        public const string ListId = "career-candidates.list";
        public const string DetailsId = "career-candidates.details";
        public const string CloseId = "career-candidates.close";

        private UiLabel _title, _count;
        private UiCardGrid _list;
        private UiScrollText _details;
        private UiButton _close;
        private readonly CardLabel _rowLabel = new CardLabel();
        private string[] _rowTexts = Array.Empty<string>();

        public CareerCandidateScreen(ScreenServices services) : base(services) { }

        public CareerCandidateModel Model { get; private set; } = CareerCandidateModel.Empty;
        /// <summary>상세에 보이는 후보 번호(-1이면 없음).</summary>
        public int DetailIndex { get; private set; } = -1;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center });
            _title.Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _count = root.Add(new UiLabel { Id = CountId, FontSize = Metrics.CountFont, Color = Metrics.Ink });
            _count.Centered(Metrics.CountX, Metrics.CountY, Metrics.CountW, Metrics.CountH);
            // 한 줄에 한 칸인 격자: 칸 하나가 목록 버튼 하나다. 스크롤 막대 자리를 더해 폭을 잡는다.
            _list = root.Add(new UiCardGrid
            {
                Id = ListId, CellWidth = Metrics.RowW, CellHeight = Metrics.RowH, Spacing = Metrics.RowSpacing, Padding = 0f, DrawBackground = false,
            });
            _list.Centered(Metrics.ListX, Metrics.ListY, Metrics.ListW + UiCardScroller.ScrollbarWidth + UiCardScroller.ScrollbarGap, Metrics.ListH);
            _list.DrawCell = PaintRow;
            _list.CellClicked += ShowDetails;
            _details = root.Add(new UiScrollText { Id = DetailsId, FontSize = Metrics.DetailsFont, Color = Metrics.Ink, PaddingX = 0f, PaddingTop = 0f, PaddingBottom = 16f });
            _details.Centered(Metrics.DetailsX, Metrics.DetailsY, Metrics.DetailsW, Metrics.DetailsH);
            _close = root.Add(new UiButton { Id = CloseId, FontSize = Metrics.CloseFont });
            _close.Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseW, Metrics.CloseH);
            _close.Clicked += Close;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            Model = CareerCandidateModel.From(Services.Session);
            DetailIndex = Model.Rows.Length > 0 ? 0 : -1;
            Show();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic != ChangeTopics.Career && topic != ChangeTopics.Day && topic != ChangeTopics.Student && topic != ChangeTopics.Language && topic != ChangeTopics.All) return;
            Model = CareerCandidateModel.From(Services.Session);
            if (DetailIndex >= Model.Rows.Length) DetailIndex = Model.Rows.Length > 0 ? 0 : -1;
            Show();
        }

        private void ShowDetails(int index)
        {
            if (index < 0 || index >= Model.Rows.Length) return;
            DetailIndex = index;
            _details.Text = DetailText();
        }

        private void Show()
        {
            _title.Text = T("ui.career-candidate.title");
            _count.Text = F("ui.career-candidate.count", Model.Rows.Length);
            _close.Text = T("facility.close");
            _list.ItemCount = Model.Rows.Length;
            _rowTexts = new string[Model.Rows.Length]; // 목록 글자는 값이 바뀔 때만 만든다
            for (int i = 0; i < _rowTexts.Length; i++) _rowTexts[i] = Shown(Model.Rows[i].DisplayName) + "\n" + T(CareerCandidateModel.StateKey(Model.Rows[i].State));
            _details.Text = DetailText();
        }

        private string DetailText()
        {
            if (DetailIndex < 0 || DetailIndex >= Model.Rows.Length) return T("ui.career-candidate.empty");
            CareerCandidateSummary s = Model.Rows[DetailIndex];
            var lines = new List<string>(16)
            {
                Shown(s.DisplayName),
                F("ui.career-candidate.state-line", T(CareerCandidateModel.StateKey(s.State))),
                F("ui.career-candidate.stats", s.HintCount, s.UnderstandingScore),
            };
            if (s.State == CareerCandidateState.Locked) lines.Add(T("ui.career-candidate.discover"));
            else if (!string.IsNullOrEmpty(s.Description)) lines.Add(Shown(s.Description));
            AddSection(lines, "ui.career-candidate.locations", s.RelatedLocationKeys, "ui.career-candidate.no-locations");
            AddSection(lines, "ui.career-candidate.activities", s.RelatedActivityKeys, "ui.career-candidate.no-activities");
            AddSection(lines, "ui.career-candidate.recommended", s.RecommendedActionKeys, "ui.career-candidate.explore");
            return JoinLines(lines);
        }

        private void AddSection(List<string> lines, string titleKey, string[] keys, string emptyKey)
        {
            lines.Add(T(titleKey));
            bool any = false;
            foreach (string key in keys ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(key)) continue;
                lines.Add(Shown(key));
                any = true;
            }
            if (!any) lines.Add(T(emptyKey));
        }

        private void PaintRow(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (index >= _rowTexts.Length) return;
            UiColor tint = state.Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(rect, UiSkin.Button, tint);
            _rowLabel.Draw(list, text, rect.Inset(2f), _rowTexts[index], Metrics.RowFont, UiTheme.Ink, lineSpacing: 0f, centerVertically: true);
            if (index == DetailIndex) list.Outline(rect, 2f, UiTheme.Accent);
            if (state.Focused && state.Selected) list.Outline(rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
