using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 들어간 직후 알림을 여는 값: 그 방이 같이 쓰는 조각 이름(방이 보낸 방 설정)과, 진행을 방에 두는 방이면 이 방에서 이어 하는지
    /// (true: 방에 그 사람의 진행이 있다, false: 처음 온 사람 — 새 캐릭터로 시작한다, null: 진행이 이 PC에 남는 방이라 알리지 않는다).
    /// </summary>
    public sealed class RoomSharesNoticeOpen
    {
        public RoomSharesNoticeOpen(IReadOnlyList<string> sharedPieces, bool? continuesHere = null)
        {
            SharedPieces = sharedPieces ?? Array.Empty<string>();
            ContinuesHere = continuesHere;
        }

        public IReadOnlyList<string> SharedPieces { get; }
        public bool? ContinuesHere { get; }
    }

    /// <summary>
    /// 남의 방에 들어간 직후 알림(멀티플레이 설계 2절): "방에 들어왔어요 · 같이 쓰는 것: 돈"과 그 아래 "이 방에서는 새 캐릭터로 시작해요" 또는
    /// "이 방에서 하던 진행을 이어 해요"(설계 3절 — 진행은 방을 가진 쪽에 남는다). 알림 층, 이동을 막지 않는다. 몇 초 보이고 닫힌다.
    /// 시간은 화면 쌓기의 Tick으로만 흐른다. 패널·모달이 열리면 바로 닫는다(설정 화면의 방 줄 등을 가리지 않게). 같은 내용은 설정 화면(Esc)의 방 줄에 계속 보인다.
    /// Host가 들어간 뒤 게임 화면을 띄울 때 연다(방을 연 사람은 창에서 골랐으므로 열지 않는다).
    /// </summary>
    public sealed class RoomSharesNoticeScreen : GameScreen
    {
        public const float ShowSeconds = 6f;

        /// <summary>배치(1920x1080 기준): 아래 가운데, 상호작용 실패 안내보다 위(HUD를 가리지 않게). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 900f, BoxH = 262f, Bottom = 340f;
            public const float TextX = 32f, TextY = 36f, Font = 20f; // 위 여백 14는 테두리 위에 글자가 걸렸다(2026-11-12)
            public const float LineH = 124f; // 같이 쓰는 것 줄(세 줄까지), 그 아래 진행 줄
        }

        public const string RootId = "room-shares-notice";
        public const string BoxId = "room-shares-notice.box";
        public const string MessageId = "room-shares-notice.message";
        public const string ProgressId = "room-shares-notice.progress";

        private UiTextBlock _message;
        private UiTextBlock _progress;
        private bool? _continues;
        private float _remaining;
        private IReadOnlyList<string> _pieces = Array.Empty<string>();

        public RoomSharesNoticeScreen(ScreenServices services) : base(services) { }

        public float RemainingSeconds => _remaining;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel, Interactive = false });
            box.Place(0.5f, 1f, -Metrics.BoxW / 2f, -Metrics.Bottom - Metrics.BoxH, Metrics.BoxW, Metrics.BoxH);
            _message = box.Add(new UiTextBlock { Id = MessageId, FontSize = Metrics.Font, Color = UiTheme.Ink, Align = UiAlign.Center });
            _message.Place(0f, 0f, Metrics.TextX, Metrics.TextY, Metrics.BoxW - Metrics.TextX * 2f, Metrics.LineH);
            _progress = box.Add(new UiTextBlock { Id = ProgressId, FontSize = Metrics.Font, Color = UiTheme.Ink, Align = UiAlign.Center });
            _progress.Place(0f, 0f, Metrics.TextX, Metrics.TextY + Metrics.LineH, Metrics.BoxW - Metrics.TextX * 2f, Metrics.BoxH - Metrics.TextY * 2f - Metrics.LineH);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!(context.Argument is RoomSharesNoticeOpen open)) { Close(); return; }
            _pieces = open.SharedPieces;
            _continues = open.ContinuesHere;
            _remaining = ShowSeconds;
            Refresh();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnTick(float seconds)
        {
            _remaining -= seconds;
            if (_remaining <= 0f || PanelOpen()) Close();
            return false;
        }

        private bool PanelOpen()
        {
            var open = Services.Screens?.OpenScreens;
            if (open == null) return false;
            for (int i = 0; i < open.Count; i++)
                if (open[i].Layer == ScreenLayer.Panel || open[i].Layer == ScreenLayer.Modal) return true;
            return false;
        }

        private void Refresh()
        {
            _message.Text = F("ui.room.joined-notice", SharesText(_pieces));
            _progress.Text = _continues == null ? string.Empty : T(_continues.Value ? "ui.room.joined-progress.continue" : "ui.room.joined-progress.new");
        }
    }
}
