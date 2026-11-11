using System;
using System.Collections.Generic;
using Rootborn.Core.Objectives;
using Rootborn.Ui;

namespace Rootborn.Screens.Objectives
{
    /// <summary>
    /// 목표 수첩(Unity ObjectiveJournalPanel, Tab). 가운데 1440x840 큰 패널: 제목, 닫기, 왼쪽 분류 줄(목표·심부름·이야기·발견),
    /// 가운데 목록(두 줄, 스크롤), 오른쪽 상세(제목·설명·진행·상태·다음 할 일·행동 결과)와 추적·행동 단추.
    /// 항목은 모으는 쪽들(ObjectiveJournalSources)이 GameSession 조회로 만든다. 이동을 막지 않는다(사용자 결정 2026-11-12).
    /// 열 때 추적 중인 항목이 있으면 그 분류와 줄을 고른다(Unity RefreshNow). 게임 진행이 바뀌면 항목을 다시 받고 고른 줄을 키로 지킨다.
    /// </summary>
    public sealed class ObjectiveJournalScreen : GameScreen, IJournalTextResolver
    {
        /// <summary>배치 — Unity ObjectiveJournalPanel.EnsureBuilt 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 1440f, Height = 840f;
            public const float TitleX = 48f, TitleY = 38f, TitleW = 700f, TitleH = 48f, TitleFont = 32f;
            public const float CloseX = 568f, CloseY = 350f, CloseW = 184f, CloseH = 58f, ButtonFont = 22f;
            public const float RailX = -570f, RailY = -30f, RailW = 200f, RailH = 650f, RailButtonTop = 250f, RailButtonStep = 130f, RailButtonH = 108f;
            public const float ListX = -230f, ListY = -30f, ListW = 420f, ListH = 650f;
            public const float DetailX = 350f, DetailY = -30f, DetailW = 640f, DetailH = 650f;
            public const float DetailInset = 8f, DetailBottom = 94f, DetailTitleFont = 28f, DetailFont = 20f;
            public const float TrackX = 160f, ActionX = -160f, DetailButtonY = -282f, DetailButtonW = 290f, DetailButtonH = 60f;
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public const string IconPrefix = "icon:";
        }

        public const string PanelId = "objective-journal";
        public const string TitleId = "objective-journal.title";
        public const string CloseId = "objective-journal.close";
        public const string ListId = "objective-journal.list";
        public const string DetailId = "objective-journal.detail";
        public const string TrackId = "objective-journal.track";
        public const string ActionId = "objective-journal.action";
        public static string CategoryButtonId(string categoryId) => "objective-journal.category." + categoryId;

        private readonly Dictionary<string, JournalRailButton> _rail = new Dictionary<string, JournalRailButton>(StringComparer.Ordinal);
        private Dictionary<string, JournalItem[]> _items = new Dictionary<string, JournalItem[]>(StringComparer.Ordinal);
        private UiLabel _title;
        private UiButton _close, _track, _action;
        private JournalList _list;
        private JournalDetail _detail;
        private string[] _rowTitles = Array.Empty<string>(), _rowProgress = Array.Empty<string>();
        private JournalAction _shownAction;
        private string _feedbackKey = string.Empty;
        private JournalText _feedback = JournalText.Empty;

        public ObjectiveJournalScreen(ScreenServices services) : base(services) { }

        public string SelectedCategory { get; private set; } = JournalCategories.Goals;
        public string SelectedStableKey { get; private set; } = string.Empty;
        public int SelectedIndex { get; private set; }
        /// <summary>지금 분류의 항목.</summary>
        public IReadOnlyList<JournalItem> Items => Current;
        public IReadOnlyDictionary<string, JournalItem[]> AllItems => _items;
        public JournalList List => _list;
        public JournalDetail Detail => _detail;

        private JournalItem[] Current => _items.TryGetValue(SelectedCategory, out JournalItem[] items) && items != null ? items : Array.Empty<JournalItem>();

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink });
            _title.Place(0f, 0f, Metrics.TitleX, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _close = root.Add(new UiButton { Id = CloseId, FontSize = Metrics.ButtonFont }).Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseW, Metrics.CloseH);
            _close.Clicked += Close;

            UiNode rail = root.Add(new UiNode { Id = "objective-journal.rail" }).Centered(Metrics.RailX, Metrics.RailY, Metrics.RailW, Metrics.RailH);
            for (int i = 0; i < JournalCategories.All.Count; i++)
            {
                JournalCategory category = JournalCategories.All[i];
                JournalRailButton button = rail.Add(new JournalRailButton { Id = CategoryButtonId(category.Id), Icon = Metrics.IconPrefix + category.IconKey, Ink = Metrics.Ink })
                    .Centered(0f, Metrics.RailButtonTop - i * Metrics.RailButtonStep, Metrics.RailW, Metrics.RailButtonH);
                button.Clicked += () => SelectCategory(category.Id);
                _rail[category.Id] = button;
            }

            _list = root.Add(new JournalList { Id = ListId, Ink = Metrics.Ink }).Centered(Metrics.ListX, Metrics.ListY, Metrics.ListW, Metrics.ListH);
            _list.RowText = row => (row < _rowTitles.Length ? _rowTitles[row] : string.Empty, row < _rowProgress.Length ? _rowProgress[row] : string.Empty);
            _list.RowClicked += SelectItem;

            var detailPanel = root.Add(new UiPanel { Id = "objective-journal.detail-panel", Skin = UiSkin.Field }).Centered(Metrics.DetailX, Metrics.DetailY, Metrics.DetailW, Metrics.DetailH);
            _detail = detailPanel.Add(new JournalDetail { Id = DetailId, Ink = Metrics.Ink });
            _detail.Stretch(Metrics.DetailInset, Metrics.DetailInset, Metrics.DetailInset, Metrics.DetailBottom);
            _track = detailPanel.Add(new UiButton { Id = TrackId, FontSize = Metrics.ButtonFont }).Centered(Metrics.TrackX, Metrics.DetailButtonY, Metrics.DetailButtonW, Metrics.DetailButtonH);
            _track.Clicked += TrackSelected;
            _action = detailPanel.Add(new UiButton { Id = ActionId, FontSize = Metrics.ButtonFont, Visible = false }).Centered(Metrics.ActionX, Metrics.DetailButtonY, Metrics.DetailButtonW, Metrics.DetailButtonH);
            _action.Clicked += RunAction;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            WriteStaticText();
            Rebuild();
            ObjectiveTracking tracking = Services.Session?.ObjectiveTracking ?? ObjectiveTracking.None;
            if (tracking.IsTracking)
            {
                JournalItem tracked = ObjectiveJournalAggregator.Find(_items, tracking.StableKey, out string category);
                if (tracked != null)
                {
                    SelectedCategory = category;
                    SelectedStableKey = tracked.StableKey;
                }
            }
            RefreshCategory();
            if (SelectedIndex >= 0 && SelectedIndex < Current.Length) _list.EnsureVisible(SelectedIndex);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Map || topic == ChangeTopics.Objectives) return;
            if (topic == ChangeTopics.Language) WriteStaticText();
            else Rebuild();
            RefreshCategory();
        }

        private void WriteStaticText()
        {
            _title.Text = T("ui.objective-journal.title");
            _close.Text = T("ui.close");
            _track.Text = T("ui.objective.track");
            foreach (JournalCategory category in JournalCategories.All) _rail[category.Id].Text = T(category.LabelKey);
        }

        private void Rebuild() => _items = new ObjectiveJournalAggregator(ObjectiveJournalSources.For(Services)).Build(Services.Session);

        public void SelectCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId) || JournalCategories.Find(categoryId) == null) return;
            SelectedCategory = categoryId;
            SelectedIndex = 0;
            SelectedStableKey = string.Empty;
            _list.ScrollOffset = 0;
            RefreshCategory();
        }

        private void SelectItem(int index)
        {
            JournalItem[] items = Current;
            if (index < 0 || index >= items.Length) return;
            SelectedIndex = index;
            SelectedStableKey = items[index].StableKey;
            _list.SelectedIndex = index;
            WriteDetail(items[index]);
        }

        private void TrackSelected()
        {
            JournalItem[] items = Current;
            if (SelectedIndex < 0 || SelectedIndex >= items.Length) return;
            JournalItem selected = items[SelectedIndex];
            SelectedStableKey = selected.StableKey;
            Services.Session?.TrackObjective(selected.CategoryId, selected.StableKey); // 저장하고 objectives를 알린다(추적 목표 HUD가 다시 읽는다)
        }

        private void RunAction()
        {
            JournalItem[] items = Current;
            if (SelectedIndex < 0 || SelectedIndex >= items.Length) return;
            JournalAction action = items[SelectedIndex].Action;
            if (action == null || !ReferenceEquals(action, _shownAction)) return;
            string key = items[SelectedIndex].StableKey;
            JournalActionResult result = action.Run(Services.Session); // 명령이 "바뀜" 알림을 내면 항목을 다시 받는다(OnChanged)
            _feedbackKey = key;
            _feedback = result.Message ?? JournalText.Empty;
            Rebuild();
            RefreshCategory();
        }

        private void RefreshCategory()
        {
            foreach (KeyValuePair<string, JournalRailButton> pair in _rail) pair.Value.Selected = pair.Key == SelectedCategory;
            JournalItem[] items = Current;
            _list.Icon = Metrics.IconPrefix + (JournalCategories.Find(SelectedCategory)?.IconKey ?? JournalCategories.All[0].IconKey);
            if (items.Length == 0)
            {
                SelectedIndex = 0;
                SelectedStableKey = string.Empty;
                _rowTitles = _rowProgress = Array.Empty<string>();
                _list.RowCount = 0;
                WriteEmptyDetail();
                return;
            }
            RestoreSelection(items);
            if (_rowTitles.Length != items.Length) { _rowTitles = new string[items.Length]; _rowProgress = new string[items.Length]; }
            for (int i = 0; i < items.Length; i++)
            {
                _rowTitles[i] = items[i].Title.Resolve(this);
                _rowProgress[i] = items[i].Progress.Resolve(this);
            }
            _list.RowCount = items.Length;
            _list.SelectedIndex = SelectedIndex;
            WriteDetail(items[SelectedIndex]);
        }

        private void RestoreSelection(JournalItem[] items)
        {
            if (!string.IsNullOrEmpty(SelectedStableKey))
            {
                for (int i = 0; i < items.Length; i++)
                    if (items[i].StableKey == SelectedStableKey) { SelectedIndex = i; return; }
                SelectedIndex = 0;
            }
            else if (SelectedIndex < 0 || SelectedIndex >= items.Length) SelectedIndex = 0;
            SelectedStableKey = items[SelectedIndex].StableKey;
        }

        private void WriteDetail(JournalItem item)
        {
            // 행동 결과 글은 같은 항목을 보이는 동안만 남는다(Unity와 같음).
            if (_feedbackKey != item.StableKey) { _feedbackKey = string.Empty; _feedback = JournalText.Empty; }
            _detail.SetParagraphs(new[]
            {
                (item.Title.Resolve(this), Metrics.DetailTitleFont),
                (item.Subtitle.Resolve(this), Metrics.DetailFont),
                (item.Progress.Resolve(this), Metrics.DetailFont),
                (item.State.Resolve(this), Metrics.DetailFont),
                (item.NextHint.Resolve(this), Metrics.DetailFont),
                (_feedback.Resolve(this), Metrics.DetailFont),
            });
            _shownAction = item.Action;
            _action.Visible = _shownAction != null;
            _action.Text = _shownAction != null ? _shownAction.Label.Resolve(this) : string.Empty;
            _track.Enabled = true;
        }

        private void WriteEmptyDetail()
        {
            JournalCategory category = JournalCategories.Find(SelectedCategory);
            _feedbackKey = string.Empty;
            _feedback = JournalText.Empty;
            _detail.SetParagraphs(new[]
            {
                (category != null ? T(category.LabelKey) : SelectedCategory, Metrics.DetailTitleFont),
                (T("ui.objective.empty"), Metrics.DetailFont),
            });
            _shownAction = null;
            _action.Visible = false;
            _track.Enabled = false;
        }

        string IJournalTextResolver.T(string key) => T(key);
        string IJournalTextResolver.F(string key, object[] args) => F(key, args);
        string IJournalTextResolver.Shown(string keyOrText) => Shown(keyOrText);
    }
}
