using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Encyclopedia;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Records
{
    /// <summary>분류 한 줄: id와 이름 문구 키.</summary>
    public readonly record struct EncyclopediaCategoryRow(string Id, string NameKey);

    /// <summary>항목 한 줄: 항목 id, 제목 문구 키(잠겼으면 잠긴 제목), 새 항목인지, 해금됐는지.</summary>
    public readonly record struct EncyclopediaRow(string EntryId, string TitleKey, bool IsNew, bool Unlocked);

    /// <summary>오른쪽 설명: 제목 키와 본문 줄 키(해금됐으면 설명 + 자세한 줄, 아니면 잠긴 힌트 하나).</summary>
    public readonly record struct EncyclopediaDetail(string TitleKey, string[] BodyKeys);

    /// <summary>
    /// 도감 화면의 표시 모델. 분류 목록은 정렬 순서(Core EncyclopediaIndex), 항목은 id 순. 해금 여부와 새 표시는 GameSession.Encyclopedia에서 읽는다.
    /// Unity 판은 한 쪽(24개)만 보였다. 여기서는 분류의 항목을 전부 목록에 넣고 보이는 줄만 그린다(UiScrollList).
    /// </summary>
    public sealed class EncyclopediaModel
    {
        private readonly EncyclopediaIndex _index;
        private readonly GameSession _session;
        private readonly List<EncyclopediaCategory> _categories;

        public EncyclopediaModel(GameSession session)
        {
            _session = session;
            _index = session == null
                ? new EncyclopediaIndex(Array.Empty<EncyclopediaCategory>(), Array.Empty<EncyclopediaEntry>())
                : new EncyclopediaIndex(session.Definitions.All<EncyclopediaCategory>().ToArray(), session.Definitions.All<EncyclopediaEntry>().ToArray());
            _categories = _index.Categories.ToList();
            Categories = _categories.Select(c => new EncyclopediaCategoryRow(c.Id, c.DisplayName)).ToArray();
        }

        public IReadOnlyList<EncyclopediaCategoryRow> Categories { get; }

        public EncyclopediaRow[] Rows(string categoryId)
        {
            EncyclopediaCategory category = _categories.Find(c => c.Id == categoryId);
            if (category == null) return Array.Empty<EncyclopediaRow>();
            IReadOnlyList<EncyclopediaEntryRow> rows = _index.GetVisibleRows(category, _session?.Encyclopedia, 0, int.MaxValue);
            var result = new EncyclopediaRow[rows.Count];
            for (int i = 0; i < result.Length; i++) result[i] = new EncyclopediaRow(rows[i].Entry.Id, rows[i].Title, rows[i].IsNew, rows[i].IsUnlocked);
            return result;
        }

        public EncyclopediaDetail Detail(string entryId)
        {
            if (!_index.TryGetEntry(entryId, out EncyclopediaEntry entry)) return default;
            if (_session?.Encyclopedia == null || !_session.Encyclopedia.IsUnlocked(entry.Id))
                return new EncyclopediaDetail(entry.LockedDisplayName, new[] { entry.LockedHint });
            var body = new List<string>(1 + entry.DetailLines.Count) { entry.Description };
            foreach (string line in entry.DetailLines) if (!string.IsNullOrEmpty(line)) body.Add(line);
            return new EncyclopediaDetail(entry.DisplayName, body.ToArray());
        }
    }

    /// <summary>
    /// 도감(HUD 버튼). Unity EncyclopediaPanel(1280x800, 가운데)을 같은 수치로 옮겼다: 위에 아이콘·제목·닫기, 왼쪽 분류 목록(300x632),
    /// 가운데 항목 목록(432x632, 스크롤), 오른쪽 설명(400x632). 분류를 고르면 그 분류의 첫 항목이 열리고, 해금된 항목을 열면 새 표시가 사라진다.
    /// 이동을 막는다(Unity: UiGameplayInputLock). 닫기는 × 버튼, HUD 도감 버튼, Esc(Unity 판에는 없던 키 — 키보드만으로 닫을 수 있게 더했다).
    /// </summary>
    public sealed class EncyclopediaScreen : GameScreen
    {
        /// <summary>배치 — 새 디자인에서는 여기만 고친다. (x, y)는 Unity 가운데 기준이거나 패널 안 왼쪽 위 기준(주석).</summary>
        internal static class Metrics
        {
            public const float PanelW = 1280f, PanelH = 800f;
            // 패널 왼쪽 위 기준
            public const float IconX = 48f, IconY = 20f, IconSize = 64f;
            public const float TitleX = 136f, TitleY = 28f, TitleW = 1000f, TitleH = 48f, TitleFont = 32f;
            public const float CloseX = 1200f, CloseY = 28f, CloseSize = 48f;
            // 가운데 기준 (x, y위로 +) 과 크기
            public const float CategoryX = -442f, CategoryY = -48f, CategoryW = 300f, CategoryH = 632f;
            public const float RowsX = -52f, RowsY = -48f, RowsW = 432f, RowsH = 632f;
            public const float DetailX = 388f, DetailY = -48f, DetailW = 400f, DetailH = 632f;
            // 분류 패널 안
            public const float CategoryButtonX = 32f, CategoryButtonY = 32f, CategoryButtonW = 236f, CategoryButtonH = 48f, CategoryStep = 64f, CategoryFont = 22f;
            public const float MarkerW = 8f;
            // 항목 목록
            public const float RowListPadding = 32f, RowHeight = 56f, RowFont = 22f;
            // 설명 패널 안
            public const float DetailTitleX = 32f, DetailTitleY = 32f, DetailTitleW = 336f, DetailTitleH = 56f, DetailTitleFont = 26f;
            public const float DetailBodyX = 32f, DetailBodyY = 96f, DetailBodyW = 336f, DetailBodyH = 488f, DetailBodyFont = 20f;
            /// <summary>닫기 기호(언어와 무관).</summary>
            public const string Icon = "icon:encyclopedia";
            public static readonly UiColor Ink = UnityLayout.Color(48f / 255f, 71f / 255f, 83f / 255f);
        }

        public const string PanelId = "encyclopedia";
        public const string TitleId = "encyclopedia.title";
        public const string CloseId = "encyclopedia.close";
        public const string ListId = "encyclopedia.rows";
        public const string DetailTitleId = "encyclopedia.detail.title";
        public const string DetailBodyId = "encyclopedia.detail.body";
        public static string CategoryId(int index) => "encyclopedia.category." + index;

        private UiLabel _title;
        private UiTextBlock _detailTitle, _detailBody;
        private UiScrollList _list;
        private UiBox _marker;
        private UiNode _categoryPanel;
        private readonly List<UiMultilineButton> _categoryButtons = new List<UiMultilineButton>();
        private EncyclopediaRow[] _rows = Array.Empty<EncyclopediaRow>();

        public EncyclopediaScreen(ScreenServices services) : base(services) { }

        public EncyclopediaModel Model { get; private set; }
        public string SelectedCategoryId { get; private set; }
        public string SelectedEntryId { get; private set; }
        public IReadOnlyList<EncyclopediaRow> Rows => _rows;

        /// <summary>목록 줄 하나의 글자(시험용으로도 열어 둔다).</summary>
        public string RowText(int index)
        {
            EncyclopediaRow row = _rows[index];
            string title = Shown(row.TitleKey);
            return row.IsNew ? F("ui.encyclopedia.row-new", Shown(row.TitleKey)) : title;
        }

        /// <summary>키보드 처음 포커스: 항목 목록에서 시작한다.</summary>
        protected internal override UiNode InitialFocus => _list;

        protected override UiNode Build()
        {
            Model = new EncyclopediaModel(Services.Session);
            var root = new UiNode { Id = "encyclopedia.root" };
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            panel.Centered(0f, 0f, Metrics.PanelW, Metrics.PanelH);

            panel.Add(new UiImage { Id = "encyclopedia.icon", Image = Metrics.Icon }).Place(0f, 0f, Metrics.IconX, Metrics.IconY, Metrics.IconSize, Metrics.IconSize);
            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _title.Place(0f, 0f, Metrics.TitleX, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            var close = panel.Add(new UiButton { Id = CloseId, Icon = Rootborn.Screens.Day.ScreenIcons.Close });
            close.Place(0f, 0f, Metrics.CloseX, Metrics.CloseY, Metrics.CloseSize, Metrics.CloseSize);
            close.Clicked += Close;

            _categoryPanel = panel.Add(new UiPanel { Id = "encyclopedia.categories", Skin = UiSkin.Panel });
            _categoryPanel.Centered(Metrics.CategoryX, Metrics.CategoryY, Metrics.CategoryW, Metrics.CategoryH);
            _marker = _categoryPanel.Add(new UiBox { Id = "encyclopedia.category.marker", Color = UiTheme.Accent });
            for (int i = 0; i < Model.Categories.Count; i++)
            {
                int index = i;
                var button = _categoryPanel.Add(new UiMultilineButton { Id = CategoryId(i), FontSize = Metrics.CategoryFont });
                button.Place(0f, 0f, Metrics.CategoryButtonX, Metrics.CategoryButtonY + i * Metrics.CategoryStep, Metrics.CategoryButtonW, Metrics.CategoryButtonH);
                button.Clicked += () => SelectCategory(Model.Categories[index].Id);
                _categoryButtons.Add(button);
            }
            // 분류가 더 늘어도 마지막 버튼 다음 id는 숨겨진 빈 자리로 둔다(시험이 "그 다음 칸은 없다"를 본다).
            var spare = _categoryPanel.Add(new UiMultilineButton { Id = CategoryId(Model.Categories.Count), Visible = false });
            spare.Place(0f, 0f, Metrics.CategoryButtonX, Metrics.CategoryButtonY + Model.Categories.Count * Metrics.CategoryStep, Metrics.CategoryButtonW, Metrics.CategoryButtonH);

            _list = panel.Add(new UiScrollList
            {
                Id = ListId, Padding = Metrics.RowListPadding, RowHeight = Metrics.RowHeight, FontSize = Metrics.RowFont, Skin = UiSkin.Panel,
            });
            _list.Centered(Metrics.RowsX, Metrics.RowsY, Metrics.RowsW, Metrics.RowsH);
            _list.RowText = RowText;
            _list.RowClicked += row => { if (row >= 0 && row < _rows.Length) SelectEntry(_rows[row].EntryId); };

            var detail = panel.Add(new UiPanel { Id = "encyclopedia.detail", Skin = UiSkin.Panel });
            detail.Centered(Metrics.DetailX, Metrics.DetailY, Metrics.DetailW, Metrics.DetailH);
            _detailTitle = detail.Add(new UiTextBlock { Id = DetailTitleId, FontSize = Metrics.DetailTitleFont, Color = Metrics.Ink });
            _detailTitle.Place(0f, 0f, Metrics.DetailTitleX, Metrics.DetailTitleY, Metrics.DetailTitleW, Metrics.DetailTitleH);
            _detailBody = detail.Add(new UiTextBlock { Id = DetailBodyId, FontSize = Metrics.DetailBodyFont, Color = Metrics.Ink });
            _detailBody.Place(0f, 0f, Metrics.DetailBodyX, Metrics.DetailBodyY, Metrics.DetailBodyW, Metrics.DetailBodyH);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _ = Root;
            _title.Text = T("ui.encyclopedia.open");
            if (SelectedCategoryId == null || !Model.Categories.Any(c => c.Id == SelectedCategoryId))
                SelectedCategoryId = Model.Categories.Count > 0 ? Model.Categories[0].Id : null;
            RefreshCategories();
            SelectCategory(SelectedCategoryId);
        }

        protected internal override void OnChanged(string topic)
        {
            if (_markingSeen) return;
            if (topic == ChangeTopics.Language || topic == ChangeTopics.Discovery || topic == ChangeTopics.All) { _title.Text = T("ui.encyclopedia.open"); RefreshCategories(); RefreshRows(keepEntry: true); }
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape || Services.Screens?.Tree.Focused is UiTextField) return false;
            Close();
            return true;
        }

        private void RefreshCategories()
        {
            for (int i = 0; i < _categoryButtons.Count; i++) _categoryButtons[i].Text = Shown(Model.Categories[i].NameKey);
        }

        private void SelectCategory(string categoryId)
        {
            SelectedCategoryId = categoryId;
            int selected = -1;
            for (int i = 0; i < Model.Categories.Count; i++) if (Model.Categories[i].Id == categoryId) selected = i;
            _marker.Visible = selected >= 0;
            if (selected >= 0)
                _marker.Place(0f, 0f, Metrics.CategoryButtonX - Metrics.MarkerW - 4f, Metrics.CategoryButtonY + selected * Metrics.CategoryStep, Metrics.MarkerW, Metrics.CategoryButtonH);
            RefreshRows(keepEntry: false);
            if (_categoryPanel.Rect.W > 0f) _marker.Layout(_categoryPanel.Rect);
        }

        private void RefreshRows(bool keepEntry)
        {
            string previous = SelectedEntryId;
            _rows = Model.Rows(SelectedCategoryId);
            _list.RowCount = _rows.Length;
            _list.ScrollOffset = 0f;
            if (_rows.Length == 0) { SelectedEntryId = null; ShowEmptyDetail(); return; }
            int at = keepEntry ? Array.FindIndex(_rows, r => r.EntryId == previous) : -1;
            SelectEntry(_rows[at >= 0 ? at : 0].EntryId);
        }

        private bool _markingSeen;

        private void SelectEntry(string entryId)
        {
            SelectedEntryId = entryId;
            // 해금된 항목을 열어 보면 새 표시가 사라진다: GameSession 명령이 저장하고 알린다. 그 알림으로 목록을 다시 만들면 스크롤이
            // 처음으로 돌아가므로, 자기 명령이 낸 알림은 건너뛰고 여기서 줄만 다시 읽는다.
            if (Services.Session?.Encyclopedia != null && Services.Session.Encyclopedia.IsNew(entryId))
            {
                _markingSeen = true;
                try { Services.Session.MarkEncyclopediaSeen(entryId); }
                finally { _markingSeen = false; }
                _rows = Model.Rows(SelectedCategoryId);
            }
            int row = Array.FindIndex(_rows, r => r.EntryId == entryId);
            _list.SelectedIndex = row;
            EncyclopediaDetail detail = Model.Detail(entryId);
            _detailTitle.Text = Shown(detail.TitleKey);
            var body = new List<string>(detail.BodyKeys.Length);
            foreach (string key in detail.BodyKeys) body.Add(Shown(key));
            _detailBody.Text = string.Join("\n", body);
        }

        private void ShowEmptyDetail()
        {
            _detailTitle.Text = T("ui.encyclopedia.empty.title");
            _detailBody.Text = T("ui.encyclopedia.empty.hint");
        }
    }
}
