using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 명령이 거절된 이유 → 문구 키. 이유는 GameSession 명령의 결과 종류와 Core 판정(StudentActivityRefusal — 시간·요일·오늘 이미 함·체력)에서 온다.
    /// 화면은 판단하지 않고 표를 찾기만 한다. 알릴 것이 없는 결과(적용됨, 같은 요청, 정의 오류)는 null. 새 종류는 표에 한 줄 더한다.
    /// </summary>
    public static class InteractionRefusals
    {
        public const string RequirementsKey = "ui.prompt.requirements-unmet";
        public const string BagFullKey = "ui.prompt.bag-full";
        /// <summary>결과가 "자원 부족"인데 Core 판정이 시간·체력이 아니라고 할 때(지금은 가방 자리 — 결과에 따로 나오지 않는다).</summary>
        public const string CannotNowKey = "ui.prompt.cannot-now";

        private static readonly Dictionary<AdultCareerResultKind, string> CareerKeys = new Dictionary<AdultCareerResultKind, string>
        {
            [AdultCareerResultKind.RequirementFailed] = RequirementsKey,
            [AdultCareerResultKind.InsufficientTime] = CannotNowKey,
            [AdultCareerResultKind.InsufficientResources] = CannotNowKey,
            [AdultCareerResultKind.InsufficientCapacity] = BagFullKey,
        };

        private static readonly Dictionary<LifeActivityResultKind, string> ActivityKeys = new Dictionary<LifeActivityResultKind, string>
        {
            [LifeActivityResultKind.RequirementFailed] = RequirementsKey,
            [LifeActivityResultKind.InsufficientResources] = CannotNowKey,
        };

        /// <summary>미니게임 알바·연습 예약의 결과. 시간·체력 쪽 거절이면 Core 판정의 키가 먼저다.</summary>
        public static string KeyFor(AdultCareerResultKind kind, StudentActivityRefusalReason reason)
        {
            if (!CareerKeys.TryGetValue(kind, out string key)) return null;
            return Specific(kind == AdultCareerResultKind.InsufficientTime || kind == AdultCareerResultKind.InsufficientResources, reason) ?? key;
        }

        /// <summary>미니게임 없는 알바·연습의 결과.</summary>
        public static string KeyFor(LifeActivityResultKind kind, StudentActivityRefusalReason reason)
        {
            if (!ActivityKeys.TryGetValue(kind, out string key)) return null;
            return Specific(kind == LifeActivityResultKind.InsufficientResources, reason) ?? key;
        }

        private static string Specific(bool aboutCosts, StudentActivityRefusalReason reason)
        {
            if (!aboutCosts || reason == StudentActivityRefusalReason.None) return null;
            string key = StudentActivityRefusal.LocalizationKey(reason);
            return string.IsNullOrEmpty(key) ? null : key;
        }

        /// <summary>이유가 있으면 알림을 연다(같은 알림이 떠 있으면 처음부터 다시 센다). 연 키를 돌려준다(없으면 null).</summary>
        public static string Show(ScreenServices services, string key)
        {
            if (string.IsNullOrEmpty(key) || services?.Screens == null || !services.Screens.Has(ScreenCatalog.InteractionRefusal)) return null;
            services.Screens.Open(ScreenCatalog.InteractionRefusal, argument: new InteractionRefusalOpen(key));
            return key;
        }
    }

    /// <summary>알림을 여는 값: 보일 문구 키.</summary>
    public sealed class InteractionRefusalOpen
    {
        public InteractionRefusalOpen(string messageKey) { MessageKey = messageKey ?? string.Empty; }
        public string MessageKey { get; }
    }

    /// <summary>
    /// 상호작용 실패 안내(Unity PlayerInteractionRouter: 눌렀는데 아무 일도 없을 때 이유를 2.5초 보였다). 알림 층, 이동을 막지 않는다.
    /// Unity 판은 대상 위의 안내 글자를 이유로 바꿨다. 여기서는 알림 층의 작은 상자에 보인다(안내 글자는 그대로).
    /// 시간은 화면 쌓기의 Tick으로만 흐르고, 다시 열면(다시 눌러 또 거절되면) 처음부터 센다.
    /// </summary>
    public sealed class InteractionRefusalNoticeScreen : GameScreen
    {
        /// <summary>Unity FailureMessageSeconds.</summary>
        public const float ShowSeconds = 2.5f;

        /// <summary>배치(1920x1080 기준): 아래 가운데, 발견 알림보다 위. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float BoxW = 760f, BoxH = 72f, Bottom = 260f;
            public const float Font = 22f; // 글자 자리는 상자 − 테마 여백(PanelArea), 그 안에서 세로 가운데
            public static readonly UiColor Ink = new UiColor(120, 52, 40);
        }

        public const string RootId = "interaction-refusal";
        public const string BoxId = "interaction-refusal.box";
        public const string MessageId = "interaction-refusal.message";

        private UiLabel _message;
        private float _remaining;

        public InteractionRefusalNoticeScreen(ScreenServices services) : base(services) { }

        /// <summary>지금 보이는 이유의 문구 키.</summary>
        public string MessageKey { get; private set; } = string.Empty;

        /// <summary>닫히기까지 남은 초.</summary>
        public float RemainingSeconds => _remaining;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel, Interactive = false });
            box.Place(0.5f, 1f, -Metrics.BoxW / 2f, -Metrics.Bottom - Metrics.BoxH, Metrics.BoxW, Metrics.BoxH);
            _message = box.Add(new UiLabel { Id = MessageId, FontSize = Metrics.Font, Color = Metrics.Ink });
            var area = new PanelArea(Metrics.BoxW, Metrics.BoxH, Services.PaddingOf(UiSkin.Panel));
            _message.Place(0f, 0f, area.Left, area.Top, area.InnerWidth, area.InnerHeight);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            MessageKey = (context.Argument as InteractionRefusalOpen)?.MessageKey ?? string.Empty;
            if (MessageKey.Length == 0) { Close(); return; }
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
            if (_remaining <= 0f) Close();
            return false;
        }

        private void Refresh() => _message.Text = MessageKey.Length == 0 ? string.Empty : T(MessageKey);
    }
}
