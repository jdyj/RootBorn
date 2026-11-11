using System.Collections.Generic;
using System.Globalization;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 내 PC에서 방 열기(설계 6절 "내 PC에서 열기"). 시작 화면 위의 모달이고 들어가기 창(<see cref="JoinServerScreen"/>)과 같은 부품·모양이다:
    /// 제목, 듣는 주소 칸("이 PC의 IP 주소:포트"), 정원(1~방 설정의 상한), 방을 열 인물(이 PC의 이야기가 있는 슬롯),
    /// 같이 쓸 것(공유 조각마다 켬/끔 한 칸 — 조각 목록은 여는 쪽이 준다) 아래 고를 수 없는 줄("시간은 항상 같이, 가방·개인 성장은 항상 각자"),
    /// 지금 단계 문구, 열기·취소.
    /// 고른 슬롯에 이미 방이 있으면(<see cref="IRoomHost.SavedRoomIn"/>) 그 방을 만들 때 정한 같이 쓸 것·정원을 보이고 잠근다(설계 10절 3번 — 만든 뒤에는 바꾸지 않는다).
    /// 정원을 적지 않은 옛 방이면 정원은 고를 수 있다. 새 방 슬롯으로 돌아가면 그 사이 고른 값으로 돌아온다.
    /// 방을 여는 일은 <see cref="IRoomHost"/>(Host)가 하고, 단계가 바뀌면 <see cref="RoomHostText.Topic"/> 알림으로 문구만 고친다.
    /// 방이 열리고 내가 들어가면 Host가 게임 화면으로 바꾼다. 방의 기록은 고른 슬롯에 저장된다(방을 연 사람의 슬롯).
    /// </summary>
    public sealed class HostRoomScreen : GameScreen
    {
        /// <summary>배치. 새 디자인에서는 여기만 고친다(가운데 기준, y 위로). 줄은 위에서 아래로 제목 → 주소 → 정원 → 인물 → 같이 쓸 것 → 고정 줄 → 단계 → 단추.</summary>
        internal static class Metrics
        {
            public const float BoxW = 900f, BoxH = 900f;
            public const float LineW = 760f;
            public const float TitleY = 390f, TitleH = 60f, TitleFont = 32f;
            public const float LabelH = 36f, LabelFont = 20f;
            public const float AddressLabelY = 322f;
            public const float FieldY = 272f, FieldH = 60f, FieldFont = 24f;
            public const int AddressMaxLength = 64;
            public const float CapacityLabelY = 212f;
            public const float CapacityY = 162f, CapacityH = 56f, CapacityFont = 20f;
            public const float CharacterLabelY = 102f;
            public const float SlotsY = 50f, SlotsH = 60f, SlotsFont = 20f;
            public const float ShareTitleY = -10f;
            /// <summary>같이 쓸 것: 조각마다 한 칸(줄 폭을 조각 수로 나눈다), 칸 안 왼쪽에 켬/끔, 그 오른쪽에 이름.</summary>
            public const float ShareY = -60f, ShareH = 48f, ToggleW = 78f, ToggleH = 36f, ShareGap = 12f, ShareFont = 20f;
            public const float AlwaysY = -116f, AlwaysH = 56f, AlwaysFont = 18f;
            public const float StatusY = -210f, StatusH = 96f, StatusFont = 18f;
            public const float ButtonX = 150f, ButtonY = -370f, ButtonW = 260f, ButtonH = 58f, ButtonFont = 22f;
            public static readonly UiColor Shade = UnityLayout.Color(0f, 0f, 0f, 0.45f);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public static readonly UiColor ErrorInk = UnityLayout.Color(0.65f, 0.24f, 0.17f);
        }

        public const string RootId = "host-room";
        public const string BoxId = "host-room.box";
        public const string TitleId = "host-room.title";
        public const string AddressLabelId = "host-room.address-label";
        public const string AddressId = "host-room.address";
        public const string CapacityLabelId = "host-room.capacity-label";
        public const string CapacityId = "host-room.capacity";
        public const string CharacterLabelId = "host-room.character-label";
        public const string SlotsId = "host-room.slots";
        public const string ShareTitleId = "host-room.share-title";
        public const string SharesId = "host-room.shares";
        public const string ShareAlwaysId = "host-room.share-always";
        public const string StatusId = "host-room.status";
        public const string OpenId = "host-room.open";
        public const string CancelId = "host-room.cancel";

        /// <summary>조각 하나의 켬/끔·이름 요소 id(조각 이름에서 만든다).</summary>
        public static string ShareToggleId(string piece) => "host-room.share." + piece;
        public static string ShareLabelId(string piece) => "host-room.share." + piece + ".label";

        private UiLabel _title, _addressLabel, _capacityLabel, _characterLabel, _shareTitle;
        private UiNode _shares;
        private UiTextBlock _always;
        private readonly List<string> _pieces = new List<string>(4);
        private readonly List<UiToggle> _toggles = new List<UiToggle>(4);
        private readonly List<UiLabel> _pieceLabels = new List<UiLabel>(4);
        private readonly HashSet<string> _chosen = new HashSet<string>(System.StringComparer.Ordinal);
        private int _chosenCapacity;
        private SavedRoom _saved;
        private UiTextField _address;
        private UiTabBar _capacity, _slots;
        private UiTextBlock _status;
        private UiButton _open, _cancel;
        private StartMenu _menu;
        private int _capacityCount;
        private readonly List<string> _slotIds = new List<string>(SaveSlots.MaxUiSlots);
        private readonly List<string> _slotNames = new List<string>(SaveSlots.MaxUiSlots);
        private readonly List<string> _capacityLabels = new List<string>(8);

        public HostRoomScreen(ScreenServices services) : base(services) { }

        /// <summary>지금 보이는 문구의 키(여는 중, 실패 이유, 이야기 없음, 어디에 저장되는지). 없으면 null.</summary>
        public string StatusKey { get; private set; }

        /// <summary>고를 수 있는 슬롯 id(이야기가 있는 것만, 슬롯 순서).</summary>
        public IReadOnlyList<string> SlotIds => _slotIds;

        /// <summary>고른 슬롯(없으면 null).</summary>
        public string SelectedSlotId => _slotIds.Count == 0 ? null : _slotIds[_slots.Selected];

        /// <summary>고른 정원(1부터). 여는 쪽이 없으면 0. 이미 있는 방이면 그 방의 정원.</summary>
        public int MaxPlayers => _capacityCount == 0 ? 0 : _capacity.Selected + 1;

        /// <summary>지금 보이는 같이 쓸 것(켜진 조각, 조각 순서). 이미 있는 방이면 그 방의 것.</summary>
        public IReadOnlyList<string> ChosenSharedPieces
        {
            get
            {
                var list = new List<string>(_pieces.Count);
                for (int i = 0; i < _pieces.Count; i++)
                    if (_toggles[i].IsOn) list.Add(_pieces[i]);
                return list;
            }
        }

        /// <summary>고른 슬롯에 이미 방이 있다(설정이 잠겨 있다).</summary>
        public bool ExistingRoom => _saved != null;

        private IRoomHost Host => _menu?.Host;

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Shade, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.BoxW, Metrics.BoxH);
            _title = box.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.TitleY, Metrics.LineW, Metrics.TitleH);
            _addressLabel = Label(box, AddressLabelId, Metrics.AddressLabelY);
            _address = box.Add(new UiTextField { Id = AddressId, MaxLength = Metrics.AddressMaxLength, FontSize = Metrics.FieldFont })
                .Centered(0f, Metrics.FieldY, Metrics.LineW, Metrics.FieldH);
            _address.Submitted += _ => Open();
            _capacityLabel = Label(box, CapacityLabelId, Metrics.CapacityLabelY);
            _capacity = box.Add(new UiTabBar { Id = CapacityId, FontSize = Metrics.CapacityFont }).Centered(0f, Metrics.CapacityY, Metrics.LineW, Metrics.CapacityH);
            _characterLabel = Label(box, CharacterLabelId, Metrics.CharacterLabelY);
            _slots = box.Add(new UiTabBar { Id = SlotsId, FontSize = Metrics.SlotsFont }).Centered(0f, Metrics.SlotsY, Metrics.LineW, Metrics.SlotsH);
            _slots.SelectionChanged += _ => ShowSlotSettings();
            _capacity.SelectionChanged += _ => { if (_saved == null || _saved.MaxPlayers <= 0) _chosenCapacity = MaxPlayers; };
            _shareTitle = Label(box, ShareTitleId, Metrics.ShareTitleY);
            _shares = box.Add(new UiNode { Id = SharesId }).Centered(0f, Metrics.ShareY, Metrics.LineW, Metrics.ShareH);
            _always = box.Add(new UiTextBlock { Id = ShareAlwaysId, FontSize = Metrics.AlwaysFont, Color = Metrics.Ink })
                .Centered(0f, Metrics.AlwaysY, Metrics.LineW, Metrics.AlwaysH);
            _status = box.Add(new UiTextBlock { Id = StatusId, FontSize = Metrics.StatusFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.StatusY, Metrics.LineW, Metrics.StatusH);
            _open = box.Add(new UiButton { Id = OpenId, FontSize = Metrics.ButtonFont }).Centered(-Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _cancel = box.Add(new UiButton { Id = CancelId, FontSize = Metrics.ButtonFont }).Centered(Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _open.Clicked += Open;
            _cancel.Clicked += CancelAndClose;
            return root;
        }

        private static UiLabel Label(UiNode box, string id, float y) =>
            box.Add(new UiLabel { Id = id, FontSize = Metrics.LabelFont, Color = Metrics.Ink }).Centered(0f, y, Metrics.LineW, Metrics.LabelH);

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            var open = context.Argument as HostRoomOpen;
            _menu = open?.Menu;
            ReadSlots();
            ReadCapacity();
            ReadPieces();
            if (open?.Address != null) _address.SetText(open.Address);
            Refresh(); // 탭을 채운 뒤에 고른다
            if (Host != null) _capacity.Selected = Clamp(Host.DefaultMaxPlayers, 1, _capacityCount) - 1;
            _chosenCapacity = MaxPlayers;
            if (open?.SlotId != null)
            {
                int i = _slotIds.IndexOf(open.SlotId);
                if (i >= 0) _slots.Selected = i;
            }
            ShowSlotSettings();
            Services.Screens?.Tree.SetFocus(_address);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == RoomHostText.Topic || topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void ReadSlots()
        {
            _slotIds.Clear();
            _slotNames.Clear();
            if (_menu == null) return;
            foreach (SaveSlotSummary slot in _menu.Slots.List())
            {
                if (!slot.Exists) continue;
                _slotIds.Add(slot.SlotId);
                _slotNames.Add(string.IsNullOrWhiteSpace(slot.Metadata.DisplayName) ? null : slot.Metadata.DisplayName);
            }
        }

        /// <summary>정원 칸: 1부터 방 설정의 상한까지 숫자(언어와 무관한 숫자라 문구 키가 아니다).</summary>
        private void ReadCapacity()
        {
            _capacityCount = Host == null ? 0 : System.Math.Max(1, Host.MaxPlayersLimit);
            _capacityLabels.Clear();
            for (int i = 1; i <= _capacityCount; i++) _capacityLabels.Add(i.ToString(CultureInfo.InvariantCulture));
            _capacity.SetTabs(_capacityLabels);
        }

        /// <summary>같이 쓸 수 있는 조각마다 켬/끔 한 칸(열 때 한 번 — 조각 목록은 여는 쪽이 준다). 처음 켜 둘 것은 --share.</summary>
        private void ReadPieces()
        {
            _chosen.Clear();
            if (Host?.DefaultSharedPieces != null) foreach (string piece in Host.DefaultSharedPieces) _chosen.Add(piece);
            var pieces = Host?.ShareablePieces;
            if (pieces == null || SamePieces(pieces)) return;
            _shares.Children.Clear();
            _pieces.Clear();
            _toggles.Clear();
            _pieceLabels.Clear();
            float cell = Metrics.LineW / System.Math.Max(1, pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                string piece = pieces[i];
                float x = i * cell;
                var toggle = _shares.Add(new UiToggle { Id = ShareToggleId(piece) });
                toggle.Place(0f, 0.5f, x, -Metrics.ToggleH / 2f, Metrics.ToggleW, Metrics.ToggleH);
                toggle.Toggled += on => Choose(piece, on);
                var label = _shares.Add(new UiLabel { Id = ShareLabelId(piece), FontSize = Metrics.ShareFont, Color = Metrics.Ink });
                label.Place(0f, 0f, x + Metrics.ToggleW + Metrics.ShareGap, 0f, cell - Metrics.ToggleW - Metrics.ShareGap * 2f, Metrics.ShareH);
                _pieces.Add(piece);
                _toggles.Add(toggle);
                _pieceLabels.Add(label);
            }
        }

        private bool SamePieces(IReadOnlyList<string> pieces)
        {
            if (pieces.Count != _pieces.Count) return false;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != _pieces[i]) return false;
            return true;
        }

        private void Choose(string piece, bool on)
        {
            if (_saved != null)
            {
                ShowSlotSettings(); // 이미 있는 방은 바꾸지 않는다
                return;
            }

            if (on) _chosen.Add(piece);
            else _chosen.Remove(piece);
        }

        /// <summary>고른 슬롯에 맞춰 같이 쓸 것·정원을 보인다: 이미 있는 방이면 저장된 것(잠금), 새 방이면 이 창에서 고른 것.</summary>
        private void ShowSlotSettings()
        {
            _saved = Host?.SavedRoomIn(SelectedSlotId);
            for (int i = 0; i < _pieces.Count; i++)
                _toggles[i].IsOn = _saved != null ? Contains(_saved.SharedPieces, _pieces[i]) : _chosen.Contains(_pieces[i]);
            if (_capacityCount > 0)
                _capacity.Selected = Clamp(_saved != null && _saved.MaxPlayers > 0 ? _saved.MaxPlayers : System.Math.Max(1, _chosenCapacity), 1, _capacityCount) - 1;
            Refresh();
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            if (list == null) return false;
            foreach (string item in list)
                if (item == value) return true;
            return false;
        }

        /// <summary>문구와 단추 상태를 지금 단계로 고친다(열 때, 단계가 바뀔 때, 언어가 바뀔 때).</summary>
        private void Refresh()
        {
            _title.Text = T("ui.host.title");
            _addressLabel.Text = T("ui.host.address");
            _address.Placeholder = T("ui.host.address.placeholder");
            _capacityLabel.Text = T("ui.host.capacity");
            _characterLabel.Text = T("ui.host.character");
            _shareTitle.Text = T("ui.host.share");
            for (int i = 0; i < _pieces.Count; i++) _pieceLabels[i].Text = T(RoomShareText.PieceKey(_pieces[i]));
            _always.Text = T("ui.host.share-always");
            _shareTitle.Visible = _shares.Visible = _pieces.Count > 0;
            var labels = new List<string>(_slotNames.Count);
            foreach (string name in _slotNames) labels.Add(name ?? T("ui.save.empty"));
            _slots.SetTabs(labels);
            _slots.Visible = _slotIds.Count > 0;
            _capacity.Visible = _capacityCount > 0;
            _open.Text = T("ui.host.open");
            _cancel.Text = T("ui.host.cancel");

            RoomJoinPhase phase = Host?.Phase ?? RoomJoinPhase.Idle;
            bool error;
            // 여는 중·실패가 먼저다(--host --slot은 이야기가 없는 새 슬롯으로도 방을 연다).
            if (Host == null) { StatusKey = null; error = false; }
            else if (phase == RoomJoinPhase.Connecting) { StatusKey = "ui.host.opening"; error = false; }
            else if (phase == RoomJoinPhase.Failed) { StatusKey = ReasonKey(Host.FailureCode); error = true; }
            else if (_slotIds.Count == 0) { StatusKey = "ui.host.no-character"; error = false; }
            else if (_saved != null) { StatusKey = "ui.host.saved-room"; error = false; }
            else { StatusKey = "ui.host.saved-in-slot"; error = false; }
            _status.Text = StatusKey == null ? string.Empty : T(StatusKey);
            _status.Color = error ? Metrics.ErrorInk : Metrics.Ink;
            bool busy = phase == RoomJoinPhase.Connecting;
            _open.Enabled = Host != null && _slotIds.Count > 0 && !busy;
            _address.Enabled = _slots.Enabled = !busy;
            _capacity.Enabled = !busy && (_saved == null || _saved.MaxPlayers <= 0);
            foreach (UiToggle toggle in _toggles) toggle.Enabled = !busy && _saved == null;
        }

        /// <summary>그 이유 코드의 문구 키. 어느 언어에도 문구가 없는 코드면 알 수 없는 이유.</summary>
        private string ReasonKey(string code)
        {
            string key = RoomJoinText.ReasonKey(code);
            foreach (string language in Services.Text.Languages)
                if (Services.Text.Has(key, language)) return key;
            return RoomJoinText.UnknownReasonKey;
        }

        private void Open()
        {
            if (Host == null || SelectedSlotId == null || Host.Phase == RoomJoinPhase.Connecting) return;
            Host.Begin((_address.Text ?? string.Empty).Trim(), MaxPlayers, SelectedSlotId, ChosenSharedPieces);
            Refresh();
        }

        private void CancelAndClose()
        {
            if (Host != null && Host.Phase == RoomJoinPhase.Connecting) Host.Cancel();
            Close();
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
