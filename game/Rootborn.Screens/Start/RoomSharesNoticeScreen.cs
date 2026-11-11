using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>들어간 직후 알림을 여는 값: 그 방이 같이 쓰는 조각 이름(방이 보낸 방 설정).</summary>
    public sealed class RoomSharesNoticeOpen
    {
        public RoomSharesNoticeOpen(IReadOnlyList<string> sharedPieces) { SharedPieces = sharedPieces ?? Array.Empty<string>(); }
        public IReadOnlyList<string> SharedPieces { get; }
    }

    /// <summary>
    /// 남의 방에 들어간 직후 알림(멀티플레이 설계 2절): "방에 들어왔어요 · 같이 쓰는 것: 돈". 알림 층, 이동을 막지 않는다. 몇 초 보이고 닫힌다.
    /// 시간은 화면 쌓기의 Tick으로만 흐른다. 패널·모달이 열리면 바로 닫는다(설정 화면의 방 줄 등을 가리지 않게). 같은 내용은 설정 화면(Esc)의 방 줄에 계속 보인다.
    /// Host가 들어간 뒤 게임 화면을 띄울 때 연다(방을 연 사람은 창에서 골랐으므로 열지 않는다).
    /// </summary>
    public sealed class RoomSharesNoticeScreen : GameScreen
    {
        public const float ShowSeconds = 6f;

        /// <summary>배치(1920x1080 기준): 아래 가운데, 상호작용 실패 안내보다 위(HUD를 가리지 않게). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 900f, BoxH = 112f, Bottom = 360f;
            public const float TextX = 32f, TextY = 14f, Font = 20f;
        }

        public const string RootId = "room-shares-notice";
        public const string BoxId = "room-shares-notice.box";
        public const string MessageId = "room-shares-notice.message";

        private UiTextBlock _message;
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
            _message.Place(0f, 0f, Metrics.TextX, Metrics.TextY, Metrics.BoxW - Metrics.TextX * 2f, Metrics.BoxH - Metrics.TextY * 2f);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!(context.Argument is RoomSharesNoticeOpen open)) { Close(); return; }
            _pieces = open.SharedPieces;
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

        private void Refresh() => _message.Text = F("ui.room.joined-notice", SharesText(_pieces));
    }
}
