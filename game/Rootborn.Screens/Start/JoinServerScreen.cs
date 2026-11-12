using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 서버 주소로 들어가기(설계 6절 "들어가는 방법" (가)). 시작 화면 위의 모달: 제목, 주소 칸, 들어갈 인물(이 PC의 이야기가 있는 슬롯),
    /// 지금 단계 문구(접속 중, 실패·거절 이유), 들어가기·취소. 접속은 <see cref="IRoomJoin"/>(Host)이 하고, 단계가 바뀌면
    /// <see cref="RoomJoinText.Topic"/> 알림으로 문구만 고친다. 들어가면 Host가 게임 화면으로 바꾼다.
    /// 임시(설계 7절 5단계 전): 고른 슬롯의 진행은 지금처럼 이 PC에 저장된다. 이야기가 없으면 들어갈 수 없다(먼저 새 이야기).
    /// </summary>
    public sealed class JoinServerScreen : GameScreen
    {
        /// <summary>배치. 새 디자인에서는 여기만 고친다(가운데 기준, y 위로).</summary>
        internal static class Metrics
        {
            public const float BoxW = 900f, BoxH = 560f;
            public const float LineW = 760f;
            public const float TitleY = 220f, TitleH = 60f, TitleFont = 32f;
            public const float AddressLabelY = 150f, LabelH = 36f, LabelFont = 20f;
            public const float FieldY = 100f, FieldH = 60f, FieldFont = 24f;
            public const int AddressMaxLength = 64;
            public const float CharacterLabelY = 40f;
            public const float SlotsY = -14f, SlotsH = 60f, SlotsFont = 20f;
            public const float StatusY = -110f, StatusH = 96f, StatusFont = 18f;
            public const float ButtonX = 150f, ButtonY = -215f, ButtonW = 260f, ButtonH = 58f, ButtonFont = 22f;
            public static readonly UiColor Shade = UnityLayout.Color(0f, 0f, 0f, 0.45f);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public static readonly UiColor ErrorInk = UnityLayout.Color(0.65f, 0.24f, 0.17f);
        }

        public const string RootId = "join-server";
        public const string BoxId = "join-server.box";
        public const string TitleId = "join-server.title";
        public const string AddressLabelId = "join-server.address-label";
        public const string AddressId = "join-server.address";
        public const string CharacterLabelId = "join-server.character-label";
        public const string SlotsId = "join-server.slots";
        public const string StatusId = "join-server.status";
        public const string ConnectId = "join-server.connect";
        public const string CancelId = "join-server.cancel";

        private UiLabel _title, _addressLabel, _characterLabel;
        private UiTextField _address;
        private UiTabBar _slots;
        private UiTextBlock _status;
        private UiButton _connect, _cancel;
        private StartMenu _menu;
        private readonly List<string> _slotIds = new List<string>(SaveSlots.MaxUiSlots);
        private readonly List<string> _slotNames = new List<string>(SaveSlots.MaxUiSlots);

        public JoinServerScreen(ScreenServices services) : base(services) { }

        /// <summary>지금 보이는 문구의 키(접속 중, 실패 이유, 이야기 없음). 없으면 null.</summary>
        public string StatusKey { get; private set; }

        /// <summary>고를 수 있는 슬롯 id(이야기가 있는 것만, 슬롯 순서).</summary>
        public IReadOnlyList<string> SlotIds => _slotIds;

        /// <summary>고른 슬롯(없으면 null).</summary>
        public string SelectedSlotId => _slotIds.Count == 0 ? null : _slotIds[_slots.Selected];

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Shade, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.BoxW, Metrics.BoxH);
            _title = box.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Math.Min(Metrics.TitleY, new PanelArea(Metrics.BoxW, Metrics.BoxH, Services.PaddingOf(UiSkin.Panel)).CenterYFromTop(Metrics.TitleH)), Metrics.LineW, Metrics.TitleH);
            _addressLabel = box.Add(new UiLabel { Id = AddressLabelId, FontSize = Metrics.LabelFont, Color = Metrics.Ink })
                .Centered(0f, Metrics.AddressLabelY, Metrics.LineW, Metrics.LabelH);
            _address = box.Add(new UiTextField { Id = AddressId, MaxLength = Metrics.AddressMaxLength, FontSize = Metrics.FieldFont })
                .Centered(0f, Metrics.FieldY, Metrics.LineW, Metrics.FieldH);
            _address.Submitted += _ => Connect();
            _characterLabel = box.Add(new UiLabel { Id = CharacterLabelId, FontSize = Metrics.LabelFont, Color = Metrics.Ink })
                .Centered(0f, Metrics.CharacterLabelY, Metrics.LineW, Metrics.LabelH);
            _slots = box.Add(new UiTabBar { Id = SlotsId, FontSize = Metrics.SlotsFont }).Centered(0f, Metrics.SlotsY, Metrics.LineW, Metrics.SlotsH);
            _status = box.Add(new UiTextBlock { Id = StatusId, FontSize = Metrics.StatusFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.StatusY, Metrics.LineW, Metrics.StatusH);
            _connect = box.Add(new UiButton { Id = ConnectId, FontSize = Metrics.ButtonFont }).Centered(-Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _cancel = box.Add(new UiButton { Id = CancelId, FontSize = Metrics.ButtonFont }).Centered(Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _connect.Clicked += Connect;
            _cancel.Clicked += CancelAndClose;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            var open = context.Argument as JoinServerOpen;
            _menu = open?.Menu;
            ReadSlots();
            if (open?.Address != null) _address.SetText(open.Address);
            Refresh(); // 탭을 채운 뒤에 고른다
            if (open?.SlotId != null)
            {
                int i = _slotIds.IndexOf(open.SlotId);
                if (i >= 0) _slots.Selected = i;
            }
            Services.Screens?.Tree.SetFocus(_address);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == RoomJoinText.Topic || topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
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

        /// <summary>문구와 단추 상태를 지금 단계로 고친다(열 때, 단계가 바뀔 때, 언어가 바뀔 때).</summary>
        private void Refresh()
        {
            _title.Text = T("ui.join.title");
            _addressLabel.Text = T("ui.join.address");
            _address.Placeholder = T("ui.join.address.placeholder");
            _characterLabel.Text = T("ui.join.character");
            var labels = new List<string>(_slotNames.Count);
            foreach (string name in _slotNames) labels.Add(name ?? T("ui.save.empty"));
            _slots.SetTabs(labels);
            _slots.Visible = _slotIds.Count > 0;
            _connect.Text = T("ui.join.connect");
            _cancel.Text = T("ui.join.cancel");

            RoomJoinPhase phase = _menu?.Join?.Phase ?? RoomJoinPhase.Idle;
            bool error;
            // 접속 중·실패가 먼저다(--join --slot은 이야기가 없는 새 슬롯으로도 접속을 시작한다).
            if (_menu?.Join == null) { StatusKey = null; error = false; }
            else if (phase == RoomJoinPhase.Connecting) { StatusKey = "ui.join.connecting"; error = false; }
            else if (phase == RoomJoinPhase.Failed) { StatusKey = ReasonKey(_menu.Join.FailureCode); error = true; }
            else if (_slotIds.Count == 0) { StatusKey = "ui.join.no-character"; error = false; }
            else { StatusKey = "ui.join.temporary-save"; error = false; }
            _status.Text = StatusKey == null ? string.Empty : T(StatusKey);
            _status.Color = error ? Metrics.ErrorInk : Metrics.Ink;
            _connect.Enabled = _menu?.Join != null && _slotIds.Count > 0 && phase != RoomJoinPhase.Connecting;
            _address.Enabled = phase != RoomJoinPhase.Connecting;
            _slots.Enabled = phase != RoomJoinPhase.Connecting;
        }

        /// <summary>그 이유 코드의 문구 키. 어느 언어에도 문구가 없는 코드면 알 수 없는 이유.</summary>
        private string ReasonKey(string code)
        {
            string key = RoomJoinText.ReasonKey(code);
            foreach (string language in Services.Text.Languages)
                if (Services.Text.Has(key, language)) return key;
            return RoomJoinText.UnknownReasonKey;
        }

        private void Connect()
        {
            if (_menu?.Join == null || SelectedSlotId == null || _menu.Join.Phase == RoomJoinPhase.Connecting) return;
            _menu.Join.Begin((_address.Text ?? string.Empty).Trim(), SelectedSlotId);
            Refresh();
        }

        private void CancelAndClose()
        {
            if (_menu?.Join != null && _menu.Join.Phase == RoomJoinPhase.Connecting) _menu.Join.Cancel();
            Close();
        }
    }

    /// <summary>
    /// 방과 연결이 끊겼다는 알림(시작 화면 위 모달): 제목, 이유, 확인. Host가 게임을 저장하고 시작 화면으로 돌아온 뒤 연다.
    /// </summary>
    public sealed class RoomNoticeScreen : GameScreen
    {
        internal static class Metrics
        {
            public const float BoxW = 760f, BoxH = 360f;
            public const float LineW = 640f;
            public const float TitleY = 100f, TitleH = 56f, TitleFont = 28f;
            public const float ReasonY = 10f, ReasonH = 100f, ReasonFont = 20f;
            public const float ButtonY = -115f, ButtonW = 260f, ButtonH = 58f, ButtonFont = 22f;
            public static readonly UiColor Shade = UnityLayout.Color(0f, 0f, 0f, 0.45f);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
        }

        public const string RootId = "room-notice";
        public const string BoxId = "room-notice.box";
        public const string TitleId = "room-notice.title";
        public const string ReasonId = "room-notice.reason";
        public const string OkId = "room-notice.ok";

        private UiLabel _title;
        private UiTextBlock _reason;
        private UiButton _ok;
        private string _code;

        public RoomNoticeScreen(ScreenServices services) : base(services) { }

        /// <summary>보이는 이유의 문구 키.</summary>
        public string ReasonKey { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Shade, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.BoxW, Metrics.BoxH);
            _title = box.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.TitleY, Metrics.LineW, Metrics.TitleH);
            _reason = box.Add(new UiTextBlock { Id = ReasonId, FontSize = Metrics.ReasonFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.ReasonY, Metrics.LineW, Metrics.ReasonH);
            _ok = box.Add(new UiButton { Id = OkId, FontSize = Metrics.ButtonFont }).Centered(0f, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _ok.Clicked += Close;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _code = (context.Argument as RoomNoticeOpen)?.ReasonCode;
            Refresh();
            Services.Screens?.Tree.SetFocus(_ok);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void Refresh()
        {
            _title.Text = T("ui.room.notice.title");
            string key = RoomJoinText.ReasonKey(_code);
            bool known = false;
            foreach (string language in Services.Text.Languages)
                if (Services.Text.Has(key, language)) { known = true; break; }
            ReasonKey = known ? key : RoomJoinText.UnknownReasonKey;
            _reason.Text = T(ReasonKey);
            _ok.Text = T("ui.room.notice.ok");
        }
    }
}
