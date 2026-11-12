using System;
using System.Globalization;
using Rootborn.Ui;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 일과 화면의 배치 수치. Unity DailyPlanPanel(BindSchedule, 1120x800 가운데)의 오프셋을 왼쪽 위 기준으로 옮겨 한 곳에 모았다.
    /// 새 디자인에서 크기·위치가 바뀌면 여기만 고친다.
    /// </summary>
    public static class DailyPlanLayout
    {
        public const float Width = 1120, Height = 800;
        public const float Margin = 40;
        public static readonly UiRect Title = new UiRect(48, 40, 800, 44);
        public const float TitleFont = 30;
        public static readonly UiRect Icon = new UiRect(48, 92, 72, 72);
        public static readonly UiRect CloseTop = new UiRect(Width - 40 - 160, 40, 160, 54);
        public static readonly UiRect TimeBudget = new UiRect(136, 100, Width - 136 - 40, 54);
        public const float TimeBudgetFont = 24;
        /// <summary>거절 이유: 시간 줄의 오른쪽(Unity 판에는 없던 줄 — 지시문이 이유를 보이라고 했다).</summary>
        public static readonly UiRect Refusal = new UiRect(560, 100, Width - 560 - 40, 54);
        public const float RefusalFont = 18;
        public static readonly UiRect Slots = new UiRect(40, 184, Width - 80, 132);
        public const float SlotSpacing = 8, SlotFont = 18;
        public const int MaxSlots = 6;
        /// <summary>일과 길 카드 격자. Unity는 폭 1040에 3칸(336x136, 간격 16). 오른쪽 스크롤 막대 자리(14)만큼 오른쪽 여백을 줄였다.</summary>
        public static readonly UiRect Routes = new UiRect(40, 332, Width - 80 + 14, Height - 98 - 332);
        public const float RouteCardWidth = 336, RouteCardHeight = 136, RouteSpacing = 16, RouteFont = 20;
        public const float RouteIconSize = 80, RouteIconLeft = 16, RouteTextLeft = 112, RouteTextInset = 16;
        public static readonly UiRect Empty = new UiRect(48, 184, Width - 96, Height - 98 - 184);
        public const float EmptyFont = 20;
        public static readonly UiRect Navigation = new UiRect(40, Height - 90, Width - 80, 54);
        public const float NavigationSpacing = 8, ButtonFont = 18;
    }

    /// <summary>
    /// 일과(계획판 상호작용). Unity DailyPlanPanel의 일정 모드: 제목·아이콘·닫기, 시간 줄, 칸 줄(시간대·넣은 길·계획/완료), 고를 수 있는 일과 길 카드(스크롤),
    /// 아래 이전 날·다음 날·닫기. 칸을 눌러 고르고, 카드를 누르면 그 칸에 넣는다(GameSession.AssignDailyPlanSlot).
    /// 넣지 못하면(같은 길을 이미 넣었거나 하루에 넣을 수 있는 횟수를 넘으면) 시간 줄 오른쪽에 이유를 보인다. 이동을 막지 않는다.
    /// </summary>
    public sealed class DailyPlanScreen : GameScreen
    {
        public const string InteractionKind = "daily-plan-board";
        public const string RootId = "daily-plan", TimeBudgetId = "daily-plan.time-budget", RefusalId = "daily-plan.refusal",
            RouteGridId = "daily-plan.routes", EmptyId = "daily-plan.empty", CloseTopId = "daily-plan.close-top",
            PreviousId = "daily-plan.previous", NextId = "daily-plan.next", CloseBottomId = "daily-plan.close";

        public static string SlotId(int index) => "daily-plan.slot." + index;
        public static string SlotTextId(int index) => "daily-plan.slot." + index + ".text";

        private UiLabel _title, _timeBudget, _refusal, _empty;
        private UiButton _closeTop, _previous, _next, _closeBottom;
        private UiNode _slotRow;
        private readonly UiButton[] _slots = new UiButton[DailyPlanLayout.MaxSlots];
        private readonly UiCenteredText[] _slotTexts = new UiCenteredText[DailyPlanLayout.MaxSlots];
        private UiCardGrid _routes;
        private int _displayedDay, _selectedSlot;
        private string _refusalKey = string.Empty;

        public DailyPlanScreen(ScreenServices services) : base(services) { }

        public DailyPlanModel Model { get; private set; }

        /// <summary>일과 길 카드 i의 화면 사각형(테스트가 포인터로 누를 자리).</summary>
        public UiRect RouteCardRect(int index) => _routes.CellRect(index);

        /// <summary>키보드 처음 포커스: 고를 일과 칸에서 시작한다.</summary>
        protected internal override UiNode InitialFocus => _routes;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, DailyPlanLayout.Width, DailyPlanLayout.Height);

            // 제목의 위는 테마 여백(PanelArea) 아래로 — 숫자 40은 테두리가 두꺼운 테마에서 글자가 테두리에 닿았다.
            UiRect title = DailyPlanLayout.Title;
            _title = Place(root.Add(new UiLabel { FontSize = DailyPlanLayout.TitleFont }), new UiRect(title.X, Math.Max(title.Y, Services.PaddingOf(UiSkin.Panel).Top), title.W, title.H));
            Place(root.Add(new UiImage { Image = ScreenIcons.Key("daily-planner") }), DailyPlanLayout.Icon);
            _closeTop = Place(root.Add(new UiButton { Id = CloseTopId, FontSize = DailyPlanLayout.ButtonFont }), DailyPlanLayout.CloseTop);
            _closeTop.Clicked += Close;
            _timeBudget = Place(root.Add(new UiLabel { Id = TimeBudgetId, FontSize = DailyPlanLayout.TimeBudgetFont }), DailyPlanLayout.TimeBudget);
            _refusal = Place(root.Add(new UiLabel { Id = RefusalId, FontSize = DailyPlanLayout.RefusalFont, Align = UiAlign.Right, Text = string.Empty }), DailyPlanLayout.Refusal);

            _slotRow = Place(root.Add(new UiNode { Id = "daily-plan.slots" }), DailyPlanLayout.Slots);
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slots[i] = _slotRow.Add(new UiButton { Id = SlotId(i), Visible = false });
                _slotTexts[i] = _slots[i].Add(new UiCenteredText { Id = SlotTextId(i), FontSize = DailyPlanLayout.SlotFont, OnSkin = UiSkin.Button });
                _slotTexts[i].Stretch(8, 3, 8, 3);
                _slots[i].Clicked += () => SelectSlot(index);
            }

            _routes = Place(root.Add(new UiCardGrid
            {
                Id = RouteGridId, Padding = 0, DrawBackground = false,
                CellWidth = DailyPlanLayout.RouteCardWidth, CellHeight = DailyPlanLayout.RouteCardHeight, Spacing = DailyPlanLayout.RouteSpacing,
            }), DailyPlanLayout.Routes);
            _routes.DrawCell = DrawRoute;
            _routes.CellClicked += AssignRoute;
            _empty = Place(root.Add(new UiLabel { Id = EmptyId, FontSize = DailyPlanLayout.EmptyFont, Align = UiAlign.Center }), DailyPlanLayout.Empty);

            UiRect nav = DailyPlanLayout.Navigation;
            float w = (nav.W - 2 * DailyPlanLayout.NavigationSpacing) / 3f;
            _previous = Place(root.Add(new UiButton { Id = PreviousId, FontSize = DailyPlanLayout.ButtonFont }), new UiRect(nav.X, nav.Y, w, nav.H));
            _next = Place(root.Add(new UiButton { Id = NextId, FontSize = DailyPlanLayout.ButtonFont }), new UiRect(nav.X + w + DailyPlanLayout.NavigationSpacing, nav.Y, w, nav.H));
            _closeBottom = Place(root.Add(new UiButton { Id = CloseBottomId, FontSize = DailyPlanLayout.ButtonFont }), new UiRect(nav.X + 2 * (w + DailyPlanLayout.NavigationSpacing), nav.Y, w, nav.H));
            _previous.Clicked += () => MoveDiary(-1);
            _next.Clicked += () => MoveDiary(1);
            _closeBottom.Clicked += Close;
            return root;
        }

        private static T Place<T>(T node, UiRect r) where T : UiNode
        {
            node.Place(0f, 0f, r.X, r.Y, r.W, r.H);
            return node;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            // 게임 한 판 없이(--world) 띄운 월드에서는 일과가 없다. 열자마자 닫는다.
            if (Services.Session == null) { Close(); return; }
            var session = Services.Session;
            _displayedDay = session.DailyPlan.CurrentDay;
            _selectedSlot = DailyPlanModel.FirstOpenSlot(session);
            _refusalKey = string.Empty;
            _routes.ScrollOffset = 0;
            Render();
        }

        protected internal override void OnChanged(string topic)
        {
            if (Services.Session != null) Render();
        }

        private void SelectSlot(int index)
        {
            _selectedSlot = index;
            _refusalKey = string.Empty;
            Render();
        }

        private void MoveDiary(int delta)
        {
            _displayedDay = DailyPlanModel.MoveDiary(Services.Session, _displayedDay, delta);
            _refusalKey = string.Empty;
            Render();
        }

        private void AssignRoute(int index)
        {
            if (Model == null || !Model.CanAssign || index < 0 || index >= Model.Routes.Count) return;
            string routeId = Model.Routes[index].RouteId;
            int slot = Model.SelectedSlot;
            bool assigned = Services.Session.AssignDailyPlanSlot(slot, routeId);
            _refusalKey = assigned ? string.Empty : "daily-plan.assign-refused";
            _refusal.Text = _refusalKey.Length == 0 ? string.Empty : T(_refusalKey);
        }

        private void Render()
        {
            var session = Services.Session;
            Model = DailyPlanModel.From(session, _displayedDay, _selectedSlot);
            _displayedDay = Model.DisplayedDay;
            _selectedSlot = Model.SelectedSlot;

            _title.Text = T("life.daily-plan.title");
            _closeTop.Text = T("daily-plan.close");
            _closeBottom.Text = T("daily-plan.close");
            _previous.Text = T("daily-plan.previous");
            _next.Text = T("daily-plan.next");
            _previous.Enabled = Model.CanGoPrevious;
            _next.Enabled = Model.CanGoNext;
            _timeBudget.Text = Model.IsCurrentDay
                ? StudentTimeText.Format(T, session.Student)
                : string.Format(CultureInfo.InvariantCulture, T("daily-plan.diary-day"), Model.DisplayedDay);
            _refusal.Text = _refusalKey.Length == 0 ? string.Empty : T(_refusalKey);

            int n = Math.Min(Model.Slots.Count, _slots.Length);
            float w = n == 0 ? 0 : (DailyPlanLayout.Slots.W - (n - 1) * DailyPlanLayout.SlotSpacing) / n;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool shown = i < n;
                _slots[i].Visible = shown;
                if (!shown) continue;
                DailyPlanSlotView slot = Model.Slots[i];
                _slots[i].Place(0f, 0f, i * (w + DailyPlanLayout.SlotSpacing), 0f, w, DailyPlanLayout.Slots.H);
                _slots[i].Enabled = slot.Interactable;
                string route = slot.RouteId.Length == 0 ? T("daily-plan.empty-slot") : T(slot.RouteNameKey);
                string status = slot.Completed ? T("daily-plan.completed") : T("daily-plan.planned");
                _slotTexts[i].Text = string.Format(CultureInfo.InvariantCulture, T(slot.Selected ? "daily-plan.slot-selected" : "daily-plan.slot"),
                    slot.Now ? F("daily-plan.period-now", T(slot.PeriodKey)) : T(slot.PeriodKey), route, status);
            }

            _routes.ItemCount = Model.Routes.Count;
            _empty.Visible = Model.Routes.Count == 0;
            _empty.Text = Model.Routes.Count == 0 ? T("objective.daily.choose.empty") : string.Empty;
            foreach (DailyPlanRouteView route in Model.Routes) T(route.NameKey);
            Root.Relayout();
        }

        private void DrawRoute(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (Model == null || index >= Model.Routes.Count) return;
            DailyPlanRouteView route = Model.Routes[index];
            bool active = state.Active && Model.CanAssign;
            UiColor tint = !active ? UiTheme.ButtonDisabled : state.Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(rect, UiSkin.Slot, tint);
            float icon = DailyPlanLayout.RouteIconSize;
            list.Image(new UiRect(rect.X + DailyPlanLayout.RouteIconLeft, rect.Y + (rect.H - icon) / 2f, icon, icon), route.Image, default, UiTheme.White);
            float inset = DailyPlanLayout.RouteTextInset;
            var label = new UiRect(rect.X + DailyPlanLayout.RouteTextLeft, rect.Y + inset, rect.W - DailyPlanLayout.RouteTextLeft - inset, rect.H - 2 * inset);
            string name = Services.Text.Get(route.NameKey);
            if (string.IsNullOrEmpty(route.NameKey)) name = Services.Text.Get("life.daily-plan.title");
            DrawMiddle(list, text, label, name, DailyPlanLayout.RouteFont, active ? UiTheme.Ink : UiTheme.InkDisabled);
        }

        private static readonly System.Collections.Generic.List<string> _wrap = new System.Collections.Generic.List<string>(4);

        private static void DrawMiddle(DrawList list, ITextMeasurer text, UiRect rect, string value, float size, UiColor color)
        {
            TextWrap.Wrap(value, rect.W, text, size, _wrap);
            float lh = text.LineHeight(size);
            int lines = Math.Min(_wrap.Count, Math.Max(1, (int)((rect.H + 2) / (lh + 2))));
            float y = rect.Y + (rect.H - (lines * lh + (lines - 1) * 2)) / 2f;
            for (int i = 0; i < lines; i++) { list.Text(rect.X, y, _wrap[i], size, color); y += lh + 2; }
        }
    }
}
