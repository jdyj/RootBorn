using System;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 집 옮김 알림(알림 층, 이동을 막지 않고 시계를 멈추지 않음): 집 맵이 새 집으로 바뀐 뒤 옛 세이브로 처음 집에 들어서면 가구를 몇 개 가까운
    /// 자리로 옮겼고 몇 개를 보관했는지 한 번 알린다(HOUSE-002). ShowSeconds가 지나면 저절로 닫힌다(화면 쌓기의 Tick으로만 흐른다).
    /// 누가 여는지는 <see cref="HouseMovedTrigger"/>. 열 때 (옮긴 수, 보관한 수)를 Argument로 받는다.
    /// </summary>
    public sealed class HouseMovedNoticeScreen : GameScreen
    {
        /// <summary>배치(1920x1080 기준): 아래 가운데. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 880f, BoxH = 120f, Bottom = 48f;
            public const float Font = 20f; // 글자 자리는 상자 − 테마 여백(PanelArea). 길면 줄이거나(FitHeight) 줄바꿈한다.
            public const float ShowSeconds = 5f;
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
        }

        public const string MessageKey = "ui.notice.house-moved";
        /// <summary>알림이 떠 있는 초.</summary>
        public const float ShowSeconds = Metrics.ShowSeconds;
        public const string RootId = "house-moved-notice", BoxId = "house-moved-notice.box", MessageId = "house-moved-notice.message";

        private UiTextBlock _message;
        private float _remaining;
        private int _moved, _stored;

        public HouseMovedNoticeScreen(ScreenServices services) : base(services) { }

        /// <summary>닫히기까지 남은 초.</summary>
        public float RemainingSeconds => _remaining;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel, Interactive = false });
            box.Place(0.5f, 1f, -Metrics.BoxW / 2f, -Metrics.Bottom - Metrics.BoxH, Metrics.BoxW, Metrics.BoxH);
            _message = box.Add(new UiTextBlock { Id = MessageId, FontSize = Metrics.Font, Color = Metrics.Ink, Align = UiAlign.Center, FitHeight = true });
            var area = new PanelArea(Metrics.BoxW, Metrics.BoxH, Services.PaddingOf(UiSkin.Panel));
            _message.Place(0f, 0f, area.Left, area.Top, area.InnerWidth, area.InnerHeight);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _remaining = Metrics.ShowSeconds;
            if (context.Argument is ValueTuple<int, int> counts) (_moved, _stored) = counts;
            ApplyText();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language) ApplyText();
        }

        private void ApplyText() => _message.Text = F(MessageKey, _moved, _stored);

        protected internal override bool OnTick(float seconds)
        {
            _remaining -= seconds;
            if (_remaining <= 0f) Close();
            return false;
        }
    }

    /// <summary>
    /// 집에 들어서며 옛 가구를 옮기거나 보관했으면 알림을 연다. 가구가 바뀌었다는 알림(<see cref="GameChangeTopics.Furniture"/>)을 받을 때
    /// GameSession에 물어 한 번만 받는다(<see cref="GameSession.TryTakeHouseRelocationNotice"/>). 규칙은 판단하지 않는다.
    /// </summary>
    public sealed class HouseMovedTrigger
    {
        private readonly ScreenServices _services;
        private readonly string _screenId;

        public HouseMovedTrigger(ScreenServices services, string screenId)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _screenId = screenId;
            if (services.Session != null) services.Session.Changed += OnChanged;
        }

        private void OnChanged(string topic)
        {
            if (topic != GameChangeTopics.Furniture) return;
            if (_services.Session.TryTakeHouseRelocationNotice(out int moved, out int stored))
                _services.Screens.Open(_screenId, null, (moved, stored));
        }
    }
}
