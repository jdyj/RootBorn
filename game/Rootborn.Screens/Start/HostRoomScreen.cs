using System.Collections.Generic;
using System.Globalization;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 내 PC에서 방 열기(설계 6절 "내 PC에서 열기"). 시작 화면 위의 모달이고 들어가기 창(<see cref="JoinServerScreen"/>)과 같은 부품·모양이다:
    /// 제목, 듣는 주소 칸("이 PC의 IP 주소:포트"), 정원(1~방 설정의 상한), 방을 열 인물(이 PC의 이야기가 있는 슬롯), 지금 단계 문구, 열기·취소.
    /// 방을 여는 일은 <see cref="IRoomHost"/>(Host)가 하고, 단계가 바뀌면 <see cref="RoomHostText.Topic"/> 알림으로 문구만 고친다.
    /// 방이 열리고 내가 들어가면 Host가 게임 화면으로 바꾼다. 방의 기록은 고른 슬롯에 저장된다(방을 연 사람의 슬롯).
    /// 방 설정(공유할 것 고르기)은 아직 없다 — 생기면 정원 줄 아래에 줄을 더한다(<see cref="Metrics"/>의 세로 자리를 함께 내린다).
    /// </summary>
    public sealed class HostRoomScreen : GameScreen
    {
        /// <summary>배치. 새 디자인에서는 여기만 고친다(가운데 기준, y 위로). 줄은 위에서 아래로 제목 → 주소 → 정원 → 인물 → 단계 → 단추.</summary>
        internal static class Metrics
        {
            public const float BoxW = 900f, BoxH = 700f;
            public const float LineW = 760f;
            public const float TitleY = 290f, TitleH = 60f, TitleFont = 32f;
            public const float LabelH = 36f, LabelFont = 20f;
            public const float AddressLabelY = 222f;
            public const float FieldY = 172f, FieldH = 60f, FieldFont = 24f;
            public const int AddressMaxLength = 64;
            public const float CapacityLabelY = 112f;
            public const float CapacityY = 62f, CapacityH = 56f, CapacityFont = 20f;
            public const float CharacterLabelY = 2f;
            public const float SlotsY = -50f, SlotsH = 60f, SlotsFont = 20f;
            public const float StatusY = -150f, StatusH = 96f, StatusFont = 18f;
            public const float ButtonX = 150f, ButtonY = -280f, ButtonW = 260f, ButtonH = 58f, ButtonFont = 22f;
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
        public const string StatusId = "host-room.status";
        public const string OpenId = "host-room.open";
        public const string CancelId = "host-room.cancel";

        private UiLabel _title, _addressLabel, _capacityLabel, _characterLabel;
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

        /// <summary>고른 정원(1부터). 여는 쪽이 없으면 0.</summary>
        public int MaxPlayers => _capacityCount == 0 ? 0 : _capacity.Selected + 1;

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
            if (open?.Address != null) _address.SetText(open.Address);
            Refresh(); // 탭을 채운 뒤에 고른다
            if (Host != null) _capacity.Selected = Clamp(Host.DefaultMaxPlayers, 1, _capacityCount) - 1;
            if (open?.SlotId != null)
            {
                int i = _slotIds.IndexOf(open.SlotId);
                if (i >= 0) _slots.Selected = i;
            }
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

        /// <summary>문구와 단추 상태를 지금 단계로 고친다(열 때, 단계가 바뀔 때, 언어가 바뀔 때).</summary>
        private void Refresh()
        {
            _title.Text = T("ui.host.title");
            _addressLabel.Text = T("ui.host.address");
            _address.Placeholder = T("ui.host.address.placeholder");
            _capacityLabel.Text = T("ui.host.capacity");
            _characterLabel.Text = T("ui.host.character");
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
            else { StatusKey = "ui.host.saved-in-slot"; error = false; }
            _status.Text = StatusKey == null ? string.Empty : T(StatusKey);
            _status.Color = error ? Metrics.ErrorInk : Metrics.Ink;
            bool busy = phase == RoomJoinPhase.Connecting;
            _open.Enabled = Host != null && _slotIds.Count > 0 && !busy;
            _address.Enabled = _capacity.Enabled = _slots.Enabled = !busy;
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
            Host.Begin((_address.Text ?? string.Empty).Trim(), MaxPlayers, SelectedSlotId);
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
