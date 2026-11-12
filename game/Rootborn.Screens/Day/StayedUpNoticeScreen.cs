using System;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 밤샘 알림(알림 층, 이동을 막지 않고 시계를 멈추지 않음): 자지 않고 날이 넘어가는 시각(07:00)을 지나 하루 결과 화면 없이 다음 날이 열렸을 때
    /// 무슨 일이 났는지 한 줄로 알린다(흐르는 시간 설계 3.4, 앞 세션 판단 6번). ShowSeconds가 지나면 저절로 닫힌다(화면 쌓기의 Tick으로만 흐른다).
    /// 누가 여는지는 <see cref="StayedUpTrigger"/>.
    /// </summary>
    public sealed class StayedUpNoticeScreen : GameScreen
    {
        /// <summary>배치(1920x1080 기준): 아래 가운데. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 720f, BoxH = 96f, Bottom = 48f;
            public const float Font = 22f; // 글자 자리는 상자 − 테마 여백(PanelArea), 그 안에서 세로 가운데
            public const float ShowSeconds = 5f;
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
        }

        public const string MessageKey = "ui.notice.stayed-up";
        /// <summary>알림이 떠 있는 초.</summary>
        public const float ShowSeconds = Metrics.ShowSeconds;
        public const string RootId = "stayed-up-notice", BoxId = "stayed-up-notice.box", MessageId = "stayed-up-notice.message";

        private UiLabel _message;
        private float _remaining;

        public StayedUpNoticeScreen(ScreenServices services) : base(services) { }

        /// <summary>닫히기까지 남은 초.</summary>
        public float RemainingSeconds => _remaining;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel, Interactive = false });
            box.Place(0.5f, 1f, -Metrics.BoxW / 2f, -Metrics.Bottom - Metrics.BoxH, Metrics.BoxW, Metrics.BoxH);
            _message = box.Add(new UiLabel { Id = MessageId, FontSize = Metrics.Font, Color = Metrics.Ink, Align = UiAlign.Center });
            var area = new PanelArea(Metrics.BoxW, Metrics.BoxH, Services.PaddingOf(UiSkin.Panel));
            _message.Place(0f, 0f, area.Left, area.Top, area.InnerWidth, area.InnerHeight);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _remaining = Metrics.ShowSeconds;
            _message.Text = T(MessageKey);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language) _message.Text = T(MessageKey);
        }

        protected internal override bool OnTick(float seconds)
        {
            _remaining -= seconds;
            if (_remaining <= 0f) Close();
            return false;
        }
    }

    /// <summary>
    /// 자지 않고 날이 넘어간 것을 알아채 밤샘 알림을 연다: 날이 바뀌어 새 날이 진행 중인데 지난밤에 자지 않았다(Core StudentLifeProgress.LastNight).
    /// 규칙은 판단하지 않는다. 하루 결과의 퀘스트·물건 변화 기준점도 새 날에서 다시 잡는다(하루 결과 화면의 다음 날 단추가 하는 일과 같다).
    /// </summary>
    public sealed class StayedUpTrigger
    {
        private readonly ScreenServices _services;
        private readonly string _screenId;
        private int _day;

        public StayedUpTrigger(ScreenServices services, string screenId)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _screenId = screenId;
            GameSession session = services.Session;
            if (session == null) return;
            _day = session.Student.CurrentDay;
            session.Changed += OnChanged;
        }

        private void OnChanged(string topic)
        {
            if (topic != GameChangeTopics.Day) return;
            GameSession session = _services.Session;
            StudentLifeProgress student = session.Student;
            if (student.CurrentDay == _day || student.DayState != StudentDayState.InProgress) return;
            _day = student.CurrentDay;
            if (student.LastNight.HasSlept) return;
            DayBaseline.For(session).Capture();
            _services.Screens.Open(_screenId);
        }
    }
}
