using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Records
{
    /// <summary>장소 기록 카드 하나: 상태 id, 제목 문구 키, 장소 이름 키, 시간대 이름 키, 날(0이면 날을 모른다).</summary>
    public readonly record struct LocationLogRow(string StateId, string TitleKey, string LocationNameKey, string TimeNameKey, int Day);

    /// <summary>
    /// 장소 기록의 표시 모델: 오늘 본 장소 상태들(LocationStateProgress.TodaySummary). Unity LocationStatePresentation의 규칙을 따른다 —
    /// 숨은 상태는 "아직 들르지 않은 곳의 기록", 정의가 있으면 그 이름 키, 없으면 기록에 남은 이름, 그것도 없으면 "방문한 장소의 변화".
    /// 장소·시간대 이름은 데이터의 displayNameKey, 못 찾으면 "마을"·"방문 시간".
    /// </summary>
    public sealed class LocationStateLogModel
    {
        public const string HiddenTitleKey = "dream.location-state.record.hidden";
        public const string UnknownTitleKey = "life.location-state.record.unknown";
        public const string DefaultLocationKey = "life.location-state.record.location";
        public const string DefaultTimeKey = "life.location-state.record.time";

        public static readonly LocationStateLogModel Empty = new LocationStateLogModel(Array.Empty<LocationLogRow>());

        public LocationStateLogModel(LocationLogRow[] rows) { Rows = rows; }

        public IReadOnlyList<LocationLogRow> Rows { get; }

        public static LocationStateLogModel From(GameSession session, DisplayNames names)
        {
            if (session?.LocationStates == null) return Empty;
            LocationStateTodaySummarySaveData[] summaries = session.LocationStates.TodaySummary;
            var rows = new LocationLogRow[summaries.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = RowFor(summaries[i], session, names);
            return new LocationStateLogModel(rows);
        }

        public static LocationLogRow RowFor(LocationStateTodaySummarySaveData summary, GameSession session, DisplayNames names)
        {
            names = names ?? DisplayNames.Empty;
            string title;
            if (summary.Hidden) title = HiddenTitleKey;
            else
            {
                string key = session.Definitions.TryGet<LocationState>(summary.StateId, out LocationState state) ? state.DisplayNameKey : summary.DisplayName;
                title = string.IsNullOrEmpty(key) ? UnknownTitleKey : key;
            }
            string location = names.Get("LocationDefinition", summary.LocationId);
            string time = names.Get("TimeSlotDefinition", summary.TimeSlotId);
            return new LocationLogRow(summary.StateId, title, string.IsNullOrEmpty(location) ? DefaultLocationKey : location,
                string.IsNullOrEmpty(time) ? DefaultTimeKey : time, summary.Day);
        }
    }

    /// <summary>
    /// 장소 기록. Unity LocationStateLogPanel(가운데 380x420)을 옮겼다: 오늘 본 장소 상태 카드(320x56) 목록과 비었을 때 안내.
    /// 카드는 제목 한 줄과 "N일차 · 장소 · 시간대" 한 줄. 이동을 막지 않는다(Unity도 입력을 잠그지 않았다).
    /// 달라진 것: Unity 판은 제목과 안내 글자가 패널 위쪽 밖에 놓였고(위 기준 앵커에 가운데 기준 좌표를 씀) 닫을 수 없었다 — 여기서는
    /// 제목·안내를 패널 안 위쪽에 놓고 Esc로 닫는다. 글자는 14 이상(제목 20·16)으로 올렸고, 카드가 많으면 스크롤한다.
    /// Unity의 "장소 기록" 버튼(오른쪽 위 168x36)은 도감 버튼 밑에 깔려 보이지 않아 HUD 버튼 표에는 없다(보고서).
    /// </summary>
    public sealed class LocationStateLogScreen : GameScreen
    {
        /// <summary>배치 — 새 디자인에서는 여기만 고친다. 가운데 기준(y는 위로 +)과 카드 안 왼쪽 위 기준.</summary>
        internal static class Metrics
        {
            public const float PanelW = 380f, PanelH = 420f;
            public const float TitleY = 176f, TitleW = 320f, TitleH = 28f, TitleFont = 20f;
            public const float EmptyY = 126f, EmptyW = 320f, EmptyH = 28f, EmptyFont = 16f;
            /// <summary>카드 목록: 패널 위에서 66 아래, 폭 320, 높이 310.</summary>
            public const float ListTop = 66f, ListW = 320f, ListH = 310f;
            public const float CardW = 320f, CardH = 56f, CardGap = 8f;
            public const float TextInset = 12f;
            public const float CardTitleTop = 5f, CardTitleH = 24f, CardTitleFont = 16f;
            public const float CardDetailTop = 30f, CardDetailH = 22f, CardDetailFont = 14f;
            public static readonly UiColor CardColor = UnityLayout.Color(0.92f, 0.82f, 0.58f, 0.92f);
            public static readonly UiColor Ink = UnityLayout.Color(0.25f, 0.18f, 0.12f);
        }

        public const string PanelId = "location-state-log";
        public const string TitleId = "location-state-log.title";
        public const string EmptyId = "location-state-log.empty";
        public const string ListId = "location-state-log.cards";

        private UiLabel _title, _empty;
        private UiCardGrid _grid;
        private string[] _titles = Array.Empty<string>(), _details = Array.Empty<string>();

        public LocationStateLogScreen(ScreenServices services) : base(services) { }

        public LocationStateLogModel Model { get; private set; } = LocationStateLogModel.Empty;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "location-state-log.root" };
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            panel.Centered(0f, 0f, Metrics.PanelW, Metrics.PanelH);
            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _title.Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _empty = panel.Add(new UiLabel { Id = EmptyId, FontSize = Metrics.EmptyFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _empty.Centered(0f, Metrics.EmptyY, Metrics.EmptyW, Metrics.EmptyH);
            _grid = panel.Add(new UiCardGrid
            {
                Id = ListId, Padding = 0f, DrawBackground = false, CellWidth = Metrics.CardW - UiCardScroller.ScrollbarWidth - UiCardScroller.ScrollbarGap,
                CellHeight = Metrics.CardH, Spacing = Metrics.CardGap, StepsPerNotch = 0.5f,
            });
            _grid.Place(0.5f, 0f, -Metrics.ListW / 2f, Metrics.ListTop, Metrics.ListW, Metrics.ListH);
            _grid.DrawCell = DrawCard;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.World || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private void Refresh()
        {
            _ = Root;
            _title.Text = T("ui.location-log.title");
            _empty.Text = T("ui.location-log.empty");
            Model = LocationStateLogModel.From(Services.Session, Services.Names);
            int count = Model.Rows.Count;
            _titles = new string[count];
            _details = new string[count];
            for (int i = 0; i < count; i++)
            {
                LocationLogRow row = Model.Rows[i];
                _titles[i] = Shown(row.TitleKey);
                _details[i] = row.Day > 0
                    ? F("life.location-state.record.detail", row.Day, Shown(row.LocationNameKey), Shown(row.TimeNameKey))
                    : F("life.location-state.record.detail-no-day", Shown(row.LocationNameKey), Shown(row.TimeNameKey));
            }
            _grid.ItemCount = count;
            _grid.ScrollOffset = 0f;
            _empty.Visible = count == 0;
        }

        private void DrawCard(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (index >= _titles.Length) return;
            list.Fill(rect, Metrics.CardColor);
            float x = rect.X + Metrics.TextInset;
            list.Text(x, rect.Y + Metrics.CardTitleTop + (Metrics.CardTitleH - text.LineHeight(Metrics.CardTitleFont)) / 2f, _titles[index], Metrics.CardTitleFont, Metrics.Ink);
            list.Text(x, rect.Y + Metrics.CardDetailTop + (Metrics.CardDetailH - text.LineHeight(Metrics.CardDetailFont)) / 2f, _details[index], Metrics.CardDetailFont, Metrics.Ink);
            if (state.Selected && state.Focused) list.Outline(rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
