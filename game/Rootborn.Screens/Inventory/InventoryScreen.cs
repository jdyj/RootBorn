using System;
using Rootborn.Core.Inventory;
using Rootborn.Ui;

namespace Rootborn.Screens.Inventory
{
    /// <summary>
    /// 가방(I). Unity ModernUiInventoryPanel을 같은 구성·수치로 옮겼다: 가운데 1040x760 패널, 위로 튀어나온 탭 넷(전체·도구·자원과 씨앗·기타),
    /// 제목, 분류 이름, 닫기, 검색 칸(+ 휴지통·정렬 칸), 5열 칸 격자(스크롤, 칸 168 간격 16), 표시할 것이 없을 때의 빈 안내,
    /// 칸을 누르면 패널 오른쪽에 뜨는 상세 팝업(그림, 이름, 수량, 설명, 닫기).
    /// 이동을 막지 않는다. 다른 큰 패널(상태·설정·목표 수첩)과 함께 열리지 않는다(층 규칙).
    /// 칸 그림은 아이콘 키 "item:&lt;아이템 id&gt;"로만 말하고 어느 PNG인지는 Host가 정한다.
    /// 휴지통·정렬 칸은 Unity 판에서도 누르면 아무 일도 없었다(그림만) — 그대로 옮겼다.
    /// </summary>
    public sealed class InventoryScreen : GameScreen
    {
        /// <summary>배치와 구성 — 새 디자인에서는 이 표만 고친다(1920x1080 논리 좌표, 패널 왼쪽 위 기준).</summary>
        public static class Layout
        {
            public const float Width = 1040f, Height = 760f;

            public const int SlotColumns = 5;
            public const float SlotSize = 168f, SlotSpacing = 16f;
            public const float GridPadX = 40f, GridPadTop = 184f;
            public const float ScrollbarWidth = 48f;
            public const float VisibleRows = 3f;
            /// <summary>격자가 차지하는 폭: 칸 5개 + 간격 + 스크롤 막대 자리(Unity InventoryInnerPanel).</summary>
            public const float GridWidth = SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing + ScrollbarWidth + 8f;
            public const float GridHeight = VisibleRows * SlotSize + (VisibleRows - 1) * SlotSpacing;

            public const float TabWidth = 128f, TabHeight = 96f, TabSpacing = 8f, TabOverlap = 36f;
            public const float TabIconSize = 56f, TabIconDown = 8f;

            public const float TitleX = 40f, TitleY = 40f, TitleW = 240f, TitleH = 44f, TitleFont = 32f;
            public const float CategoryX = 304f, CategoryY = 40f, CategoryH = 44f, CategoryFont = 24f, CategoryRightMargin = 116f;
            public const float CloseRight = 88f, CloseY = 38f, CloseSize = 48f, CloseIconSize = 32f;

            public const float SearchHeight = 56f, SearchGap = 8f, SearchIconSize = 32f, SearchFont = 24f, SearchTextX = 48f;
            public const float SearchY = GridPadTop - SlotSpacing - SearchHeight;

            public const float CountFont = 24f, CountRight = 148f, CountTop = 116f, CountHeight = 32f;
            /// <summary>작은 칸(상자 화면)에서도 수량 글자를 이보다 줄이지 않는다(1920x1080 기준 14 미만 금지).</summary>
            public const float MinSlotFont = 18f;
            public const float IconInset = 16f;

            public const float EmptyX = 72f, EmptyY = 104f, EmptyH = 288f, EmptyIconSize = 96f, EmptyIconY = 40f;
            public const float EmptyHintX = 24f, EmptyHintY = 168f, EmptyHintH = 56f, EmptyHintFont = 28f;

            public const float PopupOffsetX = 32f, PopupW = 480f, PopupH = 400f;
            public const float PopupIconX = 36f, PopupIconY = 36f, PopupIconSize = 104f;
            public const float PopupNameX = 156f, PopupNameY = 36f, PopupNameW = 280f, PopupNameH = 56f, PopupNameMaxFont = 36f, PopupNameMinFont = 24f;
            public const float PopupCountY = 96f, PopupCountH = 48f, PopupCountFont = 28f;
            public const float PopupDescX = 36f, PopupDescY = 164f, PopupDescW = 408f, PopupDescH = 140f, PopupDescFont = 28f;
            public const float PopupCloseW = 192f, PopupCloseH = 64f, PopupCloseFromBottom = 0f, PopupCloseFont = 26f;
            public const float PopupCloseLabelX = 16f, PopupCloseLabelY = 12f, PopupCloseLabelW = 112f, PopupCloseLabelH = 40f;
            public const float PopupCloseIconX = 144f, PopupCloseIconY = 16f, PopupCloseIconSize = 32f;
        }

        public const string RootId = "inventory", TabsId = "inventory.tabs", SearchId = "inventory.search", GridId = "inventory.grid",
            CloseId = "inventory.close", EmptyId = "inventory.empty", PopupId = "inventory.popup", PopupCloseId = "inventory.popup.close",
            TitleId = "inventory.title", CategoryId = "inventory.category", PopupNameId = "inventory.popup.name",
            PopupCountId = "inventory.popup.count", PopupDescId = "inventory.popup.description";

        private static readonly UiColor Ink = UnityLayout.Color(0.12f, 0.18f, 0.25f);

        private int _tab;
        private string _query = string.Empty;
        private InventoryModel _model = InventoryModel.Empty;
        private bool _subscribed;

        private UiTabBar _tabs;
        private UiLabel _title, _category, _emptyHint;
        private UiTextField _search;
        private UiCardGrid _grid;
        private UiNode _emptyState, _popup;
        private UiShrinkLabel _popupName;
        private UiLabel _popupCount, _popupCloseLabel;
        private UiTextBlock _popupDescription;
        private UiImage _popupIcon;

        public InventoryScreen(ScreenServices services) : base(services) { }

        public InventoryModel Model => _model;

        /// <summary>돈 카드가 방 지갑이다(돈을 같이 쓰는 방 — 상세의 이름이 "방 지갑").</summary>
        public bool RoomWalletShown => Services.Session != null && Services.Session.SharesWallet;
        public int Tab => _tab;
        public string Query => _query;
        public bool PopupOpen => _popup != null && _popup.Visible;

        public static string TabIconId(int index) => "inventory.tab-icon." + index;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Layout.Width, Layout.Height);

            BuildTabs(root);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Layout.TitleFont, Color = Ink });
            // 제목 줄의 위·왼쪽은 테마 여백(PanelArea)보다 안으로 들어가지 않는다.
            UiInsets pad = Services.PaddingOf(UiSkin.Panel);
            float titleY = Math.Max(Layout.TitleY, pad.Top);
            _title.Place(0f, 0f, Math.Max(Layout.TitleX, pad.Left), titleY, Layout.TitleW, Layout.TitleH);
            _category = root.Add(new UiLabel { Id = CategoryId, FontSize = Layout.CategoryFont, Color = Ink, Align = UiAlign.Right });
            _category.Place(0f, 0f, Layout.CategoryX, Math.Max(Layout.CategoryY, pad.Top), Layout.Width - Layout.CategoryX - Layout.CategoryRightMargin, Layout.CategoryH);
            BuildCloseButton(root);
            BuildSearchRow(root);
            BuildGrid(root);
            BuildEmptyState(root);
            BuildPopup(root);
            return root;
        }

        private void BuildTabs(UiNode root)
        {
            int count = InventoryFilter.Tabs.Length;
            float total = Layout.TabWidth * count + Layout.TabSpacing * (count - 1);
            float startX = (Layout.Width - total) / 2f;
            _tabs = root.Add(new UiTabBar { Id = TabsId, RaiseSelected = 0f });
            _tabs.Place(0f, 0f, startX, -(Layout.TabHeight - Layout.TabOverlap), total, Layout.TabHeight);
            var labels = new string[count];
            for (int i = 0; i < count; i++) labels[i] = string.Empty; // Unity 판 탭에는 아이콘만 있다
            _tabs.SetTabs(labels);
            _tabs.SelectionChanged += SelectTab;
            float step = total / count;
            for (int i = 0; i < count; i++)
            {
                float x = startX + i * step + (step - Layout.TabIconSize) / 2f;
                float y = -(Layout.TabHeight - Layout.TabOverlap) + (Layout.TabHeight - Layout.TabIconSize) / 2f + Layout.TabIconDown;
                root.Add(new UiImage { Id = TabIconId(i), Image = InventoryFilter.Tabs[i].IconKey }).Place(0f, 0f, x, y, Layout.TabIconSize, Layout.TabIconSize);
            }
        }

        private void BuildCloseButton(UiNode root)
        {
            var close = root.Add(new UiButton { Id = CloseId, Skin = UiSkin.Slot });
            close.Place(0f, 0f, Layout.Width - Layout.CloseRight, Layout.CloseY, Layout.CloseSize, Layout.CloseSize);
            close.Clicked += Close;
            float inset = (Layout.CloseSize - Layout.CloseIconSize) / 2f;
            close.Add(new UiImage { Image = "icon:close" }).Place(0f, 0f, inset, inset, Layout.CloseIconSize, Layout.CloseIconSize);
        }

        private void BuildSearchRow(UiNode root)
        {
            float rowRight = Layout.Width - Layout.GridPadX - Layout.ScrollbarWidth - 8f;
            float rowWidth = rowRight - Layout.GridPadX;
            float searchWidth = rowWidth - (Layout.SearchHeight + Layout.SearchGap) * 2f - Layout.SearchGap;

            _search = root.Add(new UiTextField
            {
                Id = SearchId, MaxLength = 64, FontSize = Layout.SearchFont, PaddingX = Layout.SearchTextX, Skin = UiSkin.Slot,
            });
            _search.Place(0f, 0f, Layout.GridPadX, Layout.SearchY, searchWidth, Layout.SearchHeight);
            float iconTop = (Layout.SearchHeight - Layout.SearchIconSize) / 2f;
            _search.Add(new UiImage { Image = "icon:inventory-search" }).Place(0f, 0f, Layout.SearchGap, iconTop, Layout.SearchIconSize, Layout.SearchIconSize);

            float x = Layout.GridPadX + searchWidth + Layout.SearchGap;
            SideSlot(root, "inventory.delete", "icon:inventory-delete", x);
            SideSlot(root, "inventory.filter", "icon:inventory-filter", x + Layout.SearchHeight + Layout.SearchGap);

            _search.TextChanged += text => { if (!string.Equals(text, _query, StringComparison.Ordinal)) ApplyQuery(text); };
        }

        private static void SideSlot(UiNode root, string id, string icon, float x)
        {
            var slot = root.Add(new UiPanel { Id = id, Skin = UiSkin.Slot });
            slot.Place(0f, 0f, x, Layout.SearchY, Layout.SearchHeight, Layout.SearchHeight);
            float inset = (Layout.SearchHeight - Layout.SearchIconSize) / 2f;
            slot.Add(new UiImage { Image = icon }).Place(0f, 0f, inset, inset, Layout.SearchIconSize, Layout.SearchIconSize);
        }

        private void BuildGrid(UiNode root)
        {
            _grid = root.Add(new UiCardGrid
            {
                Id = GridId, Padding = 0f, DrawBackground = false, Spacing = Layout.SlotSpacing,
                CellWidth = Layout.SlotSize, CellHeight = Layout.SlotSize, StepsPerNotch = 1f,
            });
            _grid.Place(0f, 0f, Layout.GridPadX, Layout.GridPadTop, Layout.GridWidth, Layout.GridHeight);
            _grid.DrawCell = DrawCell;
            _grid.CellClicked += OnCellClicked;
        }

        private void BuildEmptyState(UiNode root)
        {
            float width = Layout.GridWidth - Layout.ScrollbarWidth - 8f - Layout.EmptyX * 2f;
            var panel = root.Add(new UiPanel { Id = EmptyId, Skin = UiSkin.Slot });
            panel.Place(0f, 0f, Layout.GridPadX + Layout.EmptyX, Layout.GridPadTop + Layout.EmptyY, width, Layout.EmptyH);
            panel.Add(new UiImage { Image = "icon:inventory-empty" })
                .Place(0f, 0f, (width - Layout.EmptyIconSize) / 2f, Layout.EmptyIconY, Layout.EmptyIconSize, Layout.EmptyIconSize);
            _emptyHint = panel.Add(new UiLabel { FontSize = Layout.EmptyHintFont, Color = Ink, Align = UiAlign.Center });
            _emptyHint.Place(0f, 0f, Layout.EmptyHintX, Layout.EmptyHintY, width - Layout.EmptyHintX * 2f, Layout.EmptyHintH);
            _emptyState = panel;
        }

        private void BuildPopup(UiNode root)
        {
            var popup = root.Add(new UiPanel { Id = PopupId, Skin = UiSkin.Panel, Visible = false });
            popup.Centered(Layout.Width / 2f + Layout.PopupOffsetX, 0f, Layout.PopupW, Layout.PopupH);
            _popupIcon = popup.Add(new UiImage());
            _popupIcon.Place(0f, 0f, Layout.PopupIconX, Layout.PopupIconY, Layout.PopupIconSize, Layout.PopupIconSize);
            _popupName = popup.Add(new UiShrinkLabel { Id = PopupNameId, MaxFontSize = Layout.PopupNameMaxFont, MinFontSize = Layout.PopupNameMinFont, Color = Ink });
            _popupName.Place(0f, 0f, Layout.PopupNameX, Layout.PopupNameY, Layout.PopupNameW, Layout.PopupNameH);
            _popupCount = popup.Add(new UiLabel { Id = PopupCountId, FontSize = Layout.PopupCountFont, Color = Ink });
            _popupCount.Place(0f, 0f, Layout.PopupNameX, Layout.PopupCountY, Layout.PopupNameW, Layout.PopupCountH);
            _popupDescription = popup.Add(new UiTextBlock { Id = PopupDescId, FontSize = Layout.PopupDescFont, Color = Ink });
            _popupDescription.Place(0f, 0f, Layout.PopupDescX, Layout.PopupDescY, Layout.PopupDescW, Layout.PopupDescH);

            var close = popup.Add(new UiButton { Id = PopupCloseId, Skin = UiSkin.Button });
            close.Place(0.5f, 1f, -Layout.PopupCloseW / 2f, -Layout.PopupCloseH - Layout.PopupCloseFromBottom, Layout.PopupCloseW, Layout.PopupCloseH);
            close.Clicked += ClosePopup;
            _popupCloseLabel = close.Add(new UiLabel { FontSize = Layout.PopupCloseFont, Color = UiTheme.Ink, Align = UiAlign.Center });
            _popupCloseLabel.Place(0f, 0f, Layout.PopupCloseLabelX, Layout.PopupCloseLabelY, Layout.PopupCloseLabelW, Layout.PopupCloseLabelH);
            close.Add(new UiImage { Image = "icon:close" })
                .Place(0f, 0f, Layout.PopupCloseIconX, Layout.PopupCloseIconY, Layout.PopupCloseIconSize, Layout.PopupCloseIconSize);
            _popup = popup;
        }

        // ---------- 열고 닫기, 알림 ----------

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!_subscribed && Services.Session != null)
            {
                Services.Session.Inventory.OnChanged += OnInventoryChanged;
                _subscribed = true;
            }
            Refresh();
        }

        protected internal override void OnClosed()
        {
            ClosePopup();
            ResetQuery();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void OnInventoryChanged()
        {
            if (IsOpen) Refresh();
        }

        private void ResetQuery()
        {
            _query = string.Empty;
            _search?.SetText(string.Empty);
            if (_grid != null) _grid.ScrollOffset = 0f;
        }

        private void SelectTab(int index)
        {
            _tab = index;
            ClosePopup();
            Refresh();
            _grid.ScrollOffset = 0f;
        }

        private void ApplyQuery(string text)
        {
            _query = text ?? string.Empty;
            Refresh();
            _grid.ScrollOffset = 0f;
        }

        private string NameOf(string key) => string.IsNullOrEmpty(key) ? string.Empty : Services.Text.Get(key);

        private void Refresh()
        {
            ClosePopup();
            _title.Text = T("ui.inventory.title");
            _category.Text = T(InventoryFilter.Tabs[_tab].CategoryKey);
            _search.Placeholder = T("ui.inventory.search");
            _emptyHint.Text = T("ui.inventory.empty");
            _popupCloseLabel.Text = T("ui.inventory.close-detail");
            _tabs.Selected = _tab;

            _model = InventoryModel.From(Services.Session, NameOf, _tab, _query);
            bool hasMatches = _model.Rows.Count > 0;
            // Unity 판: 맞는 것이 있으면 칸 틀 전부(빈 칸 포함)를 보이고, 없으면 빈 안내만 보인다.
            // 돈 카드는 칸이 아니라서 줄이 칸 수보다 하나 많을 수 있다.
            _grid.ItemCount = hasMatches ? Math.Max(ItemInventory.MaxSlots, _model.Rows.Count) : 0;
            _grid.Visible = hasMatches;
            _emptyState.Visible = !hasMatches;
        }

        // ---------- 칸, 상세 팝업 ----------

        private void DrawCell(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            bool hasItem = index < _model.Rows.Count;
            DrawItemSlot(rect, hasItem ? _model.Rows[index].Item : null, hasItem ? _model.Rows[index].Count : 0, state, list, text);
        }

        /// <summary>
        /// 아이템 칸 하나(가방 칸과 같은 모양 — 상자 화면도 이것을 쓴다): 칸 바탕, 그림, 수량(2 이상), 포커스 테두리. 크기는 칸에 비례한다(가방 칸 168 기준).
        /// item이 null이면 빈 칸.
        /// </summary>
        public static void DrawItemSlot(UiRect rect, ItemDefinition item, int count, CardState state, DrawList list, ITextMeasurer text)
        {
            bool hasItem = item != null && count > 0;
            list.Panel(rect, UiSkin.Slot, hasItem && state.Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
            if (hasItem)
            {
                float k = rect.W / Layout.SlotSize;
                float inset = Layout.IconInset * k;
                var icon = new UiRect(rect.X + inset, rect.Y + inset, rect.W - inset * 2f, rect.H - inset * 2f);
                list.Image(icon, ItemIconId(item), default, UiTheme.White);
                if (count > 1)
                {
                    string shown = count.ToString();
                    float font = System.Math.Max(Layout.MinSlotFont, Layout.CountFont * k);
                    float x = rect.X + Layout.CountRight * k - text.Width(shown, font);
                    float y = rect.Y + Layout.CountTop * k + (Layout.CountHeight * k - text.LineHeight(font)) / 2f;
                    list.Text(x, y, shown, font, Ink);
                }
            }
        }

        /// <summary>아이템 그림 키. 어느 PNG의 어디인지는 Host가 정한다.</summary>
        public static string ItemIconId(ItemDefinition item) => "item:" + item.Id;

        private void OnCellClicked(int index)
        {
            if (index < 0 || index >= _model.Rows.Count) return; // 빈 칸은 눌러도 아무 일도 없다
            OpenPopup(_model.Rows[index]);
        }

        private void OpenPopup(InventoryRow row)
        {
            _popupIcon.Image = ItemIconId(row.Item);
            bool roomWallet = RoomWalletShown && ReferenceEquals(row.Item, Services.Session.Inventory.Currency);
            _popupName.Text = roomWallet ? T("ui.inventory.room-wallet") : NameOf(row.Item.DisplayKey);
            if (!string.IsNullOrEmpty(row.Item.DisplayKey)) T(row.Item.DisplayKey);
            _popupCount.Text = F("ui.inventory.item-count", row.Count);
            _popupDescription.Text = Shown(row.Item.Description);
            _popup.Visible = true;
        }

        private void ClosePopup()
        {
            if (_popup == null) return;
            if (_popup.Visible && Services.Screens?.Tree.Focused != null && IsInside(Services.Screens.Tree.Focused, _popup))
                Services.Screens.Tree.SetFocus(null);
            _popup.Visible = false;
        }

        private static bool IsInside(UiNode node, UiNode root)
        {
            for (UiNode n = node; n != null; n = n.Parent) if (n == root) return true;
            return false;
        }
    }

}
