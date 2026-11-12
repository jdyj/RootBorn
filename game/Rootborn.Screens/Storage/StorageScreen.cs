using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Screens.Inventory;
using Rootborn.Ui;

namespace Rootborn.Screens.Storage
{
    /// <summary>상자 화면의 줄 하나: 아이템과 수량.</summary>
    public readonly record struct StorageRow(ItemDefinition Item, int Count);

    /// <summary>
    /// 상자 화면의 표시 모델: 왼쪽 가방 칸(돈은 지갑에 있어 빼고), 오른쪽 상자 칸, 상자 칸 수와 찬 칸 수, 같이 쓰는 상자의 기다림.
    /// GameSession 조회(Inventory, StorageAt)로 만든다. 상자가 아니거나 없으면 <see cref="Exists"/>가 false.
    /// </summary>
    public sealed class StorageModel
    {
        public static readonly StorageModel Empty = new StorageModel(new List<StorageRow>(), new List<StorageRow>(), 0, 0, false, false);

        public StorageModel(IReadOnlyList<StorageRow> bag, IReadOnlyList<StorageRow> chest, int chestSlots, int chestUsed, bool exists, bool waiting)
        {
            Bag = bag;
            Chest = chest;
            ChestSlots = chestSlots;
            ChestUsed = chestUsed;
            Exists = exists;
            Waiting = waiting;
        }

        public IReadOnlyList<StorageRow> Bag { get; }
        public IReadOnlyList<StorageRow> Chest { get; }
        public int ChestSlots { get; }
        public int ChestUsed { get; }
        public bool Exists { get; }
        public bool Waiting { get; }

        public static StorageModel From(Rootborn.Game.GameSession session, string chestId)
        {
            if (session == null) return Empty;
            StorageContents contents = session.StorageAt(chestId);
            var bag = new List<StorageRow>(ItemInventory.MaxSlots);
            ItemDefinition currency = session.Inventory.Currency;
            foreach (ItemInventory.Slot slot in session.Inventory.Slots)
                if (slot.Item != null && slot.Count > 0 && !ReferenceEquals(slot.Item, currency)) bag.Add(new StorageRow(slot.Item, slot.Count));
            if (contents == null) return new StorageModel(bag, new List<StorageRow>(), 0, 0, false, session.WaitingForStorage);
            var chest = new List<StorageRow>(contents.Stacks.Count);
            foreach (ItemStack<ItemDefinition> s in contents.Stacks)
                if (s.Item != null && s.Count > 0) chest.Add(new StorageRow(s.Item, s.Count));
            return new StorageModel(bag, chest, contents.MaxSlots, contents.UsedSlots, true, session.WaitingForStorage);
        }
    }

    /// <summary>
    /// 상자(놓인 상자 앞에서 상호작용). 왼쪽은 가방, 오른쪽은 상자 — 칸은 가방 화면과 같은 부품(UiCardGrid, InventoryScreen.DrawItemSlot)이다.
    /// 가방 칸을 누르면(포인터, 또는 Shift+방향 키로 골라 Enter) 그 칸을 통째로 상자에 넣고, 상자 칸을 누르면 가방으로 꺼낸다.
    /// 옮기기는 전부 아니면 전무이고(받는 쪽에 자리가 없으면 아무것도 바뀌지 않는다) 결과를 아래 한 줄로 알린다.
    /// 같이 쓰는 상자는 방의 허락을 기다리는 동안 옮기기를 막는다. 이동을 막지 않는다(가방과 같다). 긴 목록은 보이는 줄만 그린다.
    /// </summary>
    public sealed class StorageScreen : GameScreen
    {
        /// <summary>이 화면을 여는 상호작용 종류(Host가 놓인 상자마다 후보를 만든다).</summary>
        public const string InteractionKind = "storage";

        /// <summary>배치 — 새 디자인에서는 이 표만 고친다(1920x1080 논리 좌표, 패널 왼쪽 위 기준).</summary>
        public static class Layout
        {
            public const float Width = 1520f, Height = 820f;
            public const float Pad = 40f, ColumnGap = 48f;
            public const float TitleY = 36f, TitleH = 48f, TitleFont = 32f;
            public const float CloseRight = 88f, CloseY = 38f, CloseSize = 48f, CloseIconSize = 32f;
            public const float HeaderY = 104f, HeaderH = 40f, HeaderFont = 24f;
            public const float GridY = 156f;
            public const float SlotSize = 112f, SlotSpacing = 12f;
            public const int VisibleRows = 4;
            public const float GridHeight = VisibleRows * SlotSize + (VisibleRows - 1) * SlotSpacing;
            public const float ColumnWidth = (Width - Pad * 2f - ColumnGap) / 2f;
            public const float NoticeY = GridY + GridHeight + 20f, NoticeH = 64f, NoticeFont = 24f;
            public const float HintY = NoticeY + NoticeH + 4f, HintH = 36f, HintFont = 20f;
        }

        public const string RootId = "storage", TitleId = "storage.title", CloseId = "storage.close", BagHeaderId = "storage.bag-header",
            ChestHeaderId = "storage.chest-header", BagGridId = "storage.bag", ChestGridId = "storage.chest", NoticeId = "storage.notice", HintId = "storage.hint";

        private static readonly UiColor Ink = UnityLayout.Color(0.12f, 0.18f, 0.25f);

        private string _chestId;
        private StorageModel _model = StorageModel.Empty;
        private UiLabel _title, _bagHeader, _chestHeader, _hint;
        private UiTextBlock _notice;
        private UiCardGrid _bagGrid, _chestGrid;
        private string _noticeKey;

        public StorageScreen(ScreenServices services) : base(services) { }

        public StorageModel Model => _model;
        public string ChestId => _chestId;
        /// <summary>아래 안내 줄의 문구 키(없으면 null).</summary>
        public string NoticeKey => _noticeKey;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Layout.Width, Layout.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Layout.TitleFont, Color = Ink });
            _title.Place(0f, 0f, Layout.Pad, Layout.TitleY, Layout.Width - Layout.Pad * 2f - Layout.CloseRight, Layout.TitleH);

            var close = root.Add(new UiButton { Id = CloseId, Skin = UiSkin.Slot });
            close.Place(0f, 0f, Layout.Width - Layout.CloseRight, Layout.CloseY, Layout.CloseSize, Layout.CloseSize);
            close.Clicked += Close;
            float inset = (Layout.CloseSize - Layout.CloseIconSize) / 2f;
            close.Add(new UiImage { Image = "icon:close" }).Place(0f, 0f, inset, inset, Layout.CloseIconSize, Layout.CloseIconSize);

            float rightX = Layout.Pad + Layout.ColumnWidth + Layout.ColumnGap;
            _bagHeader = root.Add(new UiLabel { Id = BagHeaderId, FontSize = Layout.HeaderFont, Color = Ink });
            _bagHeader.Place(0f, 0f, Layout.Pad, Layout.HeaderY, Layout.ColumnWidth, Layout.HeaderH);
            _chestHeader = root.Add(new UiLabel { Id = ChestHeaderId, FontSize = Layout.HeaderFont, Color = Ink });
            _chestHeader.Place(0f, 0f, rightX, Layout.HeaderY, Layout.ColumnWidth, Layout.HeaderH);

            _bagGrid = root.Add(NewGrid(BagGridId));
            _bagGrid.Place(0f, 0f, Layout.Pad, Layout.GridY, Layout.ColumnWidth, Layout.GridHeight);
            _bagGrid.DrawCell = (i, r, s, l, t) => DrawRow(_model.Bag, i, r, s, l, t);
            _bagGrid.CellClicked += i => Move(i, put: true);

            _chestGrid = root.Add(NewGrid(ChestGridId));
            _chestGrid.Place(0f, 0f, rightX, Layout.GridY, Layout.ColumnWidth, Layout.GridHeight);
            _chestGrid.DrawCell = (i, r, s, l, t) => DrawRow(_model.Chest, i, r, s, l, t);
            _chestGrid.CellClicked += i => Move(i, put: false);

            _notice = root.Add(new UiTextBlock { Id = NoticeId, FontSize = Layout.NoticeFont, Color = Ink });
            _notice.Place(0f, 0f, Layout.Pad, Layout.NoticeY, Layout.Width - Layout.Pad * 2f, Layout.NoticeH);
            _hint = root.Add(new UiLabel { Id = HintId, FontSize = Layout.HintFont, Color = Ink });
            _hint.Place(0f, 0f, Layout.Pad, Layout.HintY, Layout.Width - Layout.Pad * 2f, Layout.HintH);
            return root;
        }

        private static UiCardGrid NewGrid(string id) => new UiCardGrid
        {
            Id = id, Padding = 0f, DrawBackground = false, Spacing = Layout.SlotSpacing, CellWidth = Layout.SlotSize, CellHeight = Layout.SlotSize, StepsPerNotch = 1f,
        };

        private static void DrawRow(IReadOnlyList<StorageRow> rows, int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            bool has = index < rows.Count;
            InventoryScreen.DrawItemSlot(rect, has ? rows[index].Item : null, has ? rows[index].Count : 0, state, list, text);
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _chestId = context.Interaction?.TargetId ?? context.Argument as string;
            _noticeKey = null;
            _bagGrid.ScrollOffset = 0f;
            _chestGrid.ScrollOffset = 0f;
            Refresh();
            if (!_model.Exists) Close();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic != ChangeTopics.Storage && topic != ChangeTopics.Inventory && topic != ChangeTopics.Furniture
                && topic != ChangeTopics.Language && topic != ChangeTopics.All) return;
            if (topic == ChangeTopics.Storage) ShowResult();
            Refresh();
            if (!_model.Exists) Close(); // 같이 쓰는 집에서 누가 치웠다
        }

        private void Refresh()
        {
            _model = StorageModel.From(Services.Session, _chestId);
            _title.Text = T("ui.storage.title");
            _bagHeader.Text = T("ui.storage.bag");
            _chestHeader.Text = F("ui.storage.chest", _model.ChestUsed, _model.ChestSlots);
            _hint.Text = T("ui.storage.hint");
            _notice.Text = _model.Waiting ? T("ui.storage.waiting") : _noticeKey == null ? string.Empty : T(_noticeKey);
            _bagGrid.ItemCount = System.Math.Max(ItemInventory.MaxSlots, _model.Bag.Count);
            _chestGrid.ItemCount = System.Math.Max(_model.ChestSlots, _model.Chest.Count);
        }

        private void Move(int index, bool put)
        {
            var session = Services.Session;
            IReadOnlyList<StorageRow> rows = put ? _model.Bag : _model.Chest;
            if (session == null || index < 0 || index >= rows.Count || _model.Waiting) return; // 빈 칸은 눌러도 아무 일도 없다
            StorageRow row = rows[index];
            var result = put ? session.PutIntoStorage(_chestId, row.Item.Id, row.Count) : session.TakeFromStorage(_chestId, row.Item.Id, row.Count);
            _noticeKey = NoticeFor(result);
            Refresh();
        }

        // 같이 쓰는 상자: 허락·거절이 온 뒤의 결과.
        private void ShowResult()
        {
            var result = Services.Session?.LastStorageResult;
            if (result.HasValue && !Services.Session.WaitingForStorage) _noticeKey = NoticeFor(result.Value);
        }

        /// <summary>결과 → 안내 문구 키(없으면 null). 키 이름은 결과 이름에서 만들지 않고 표로 둔다(문구 키가 모든 언어에 있는지 테스트가 본다).</summary>
        public static string NoticeFor(StorageTransferResult result)
        {
            switch (result)
            {
                case StorageTransferResult.Moved: return "ui.storage.moved";
                case StorageTransferResult.NoRoom: return "ui.storage.no-room";
                case StorageTransferResult.NotEnough: return "ui.storage.not-enough";
                case StorageTransferResult.Currency: return "ui.storage.currency";
                case StorageTransferResult.Requested: return "ui.storage.waiting";
                case StorageTransferResult.Busy: return "ui.storage.waiting";
                case StorageTransferResult.AlreadyDone: return null;
                default: return "ui.storage.not-storage";
            }
        }

        /// <summary>화면이 쓸 수 있는 모든 안내 키(문구 테스트용).</summary>
        public static readonly string[] NoticeKeys =
        {
            "ui.storage.moved", "ui.storage.no-room", "ui.storage.not-enough", "ui.storage.currency", "ui.storage.waiting", "ui.storage.not-storage",
        };
    }
}
