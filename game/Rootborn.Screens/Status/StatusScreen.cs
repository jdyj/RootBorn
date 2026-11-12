using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Status
{
    /// <summary>
    /// 상태 화면의 표시 모델: 며칠째인지, 지금 시각(보이는 0~1439분), 체력·집중·스트레스. 게임 한 판이 없으면 HasGame이 false다
    /// (Unity 판의 "상태를 불러오는 중" 모양).
    /// </summary>
    public sealed class StatusModel
    {
        public static readonly StatusModel Empty = new StatusModel(false, 0, 0, 0, 0, 0);

        public StatusModel(bool hasGame, int day, int clockMinutes, int energy, int focus, int stress)
        {
            HasGame = hasGame; Day = day; ClockMinutes = clockMinutes; Energy = energy; Focus = focus; Stress = stress;
        }

        public bool HasGame { get; }
        public int Day { get; }
        /// <summary>보이는 시각(0~1439분). 시간 포인트는 보이지 않는다(흐르는 시간 설계 4절).</summary>
        public int ClockMinutes { get; }
        public int Energy { get; }
        public int Focus { get; }
        public int Stress { get; }

        public static StatusModel From(GameSession session)
        {
            if (session == null) return Empty;
            return new StatusModel(true, session.StudentDay, Rootborn.Screens.Day.StudentTimeText.ClockMinutes(session.Student.TimeMinutes),
                session.Student.Energy, session.Student.Focus, session.Student.Stress);
        }
    }

    /// <summary>
    /// 상태(C). Unity ModernUiStatusPanel을 같은 구성·수치로 옮겼다: 가운데 560x540 패널, 제목, "N일차 · 14:05"(흐르는 시간 — 남은 활동 점수 대신 지금 시각), 닫기,
    /// 체력·집중·스트레스 카드 셋(아이콘, 이름, 값), 아래 닫기 안내. 이동을 막지 않는다.
    /// 값은 열 때와 학생 상태·하루·언어 알림을 받을 때만 고친다(매 프레임 읽지 않는다).
    /// </summary>
    public sealed class StatusScreen : GameScreen
    {
        /// <summary>배치와 구성 — 새 디자인에서는 이 표만 고친다(1920x1080 논리 좌표, 패널 왼쪽 위 기준).</summary>
        public static class Layout
        {
            public const float Width = 560f, Height = 540f;
            // 패널 안 여백은 테마(ScreenServices.PaddingOf(panel))에서 온다. 아래 값은 그 안쪽 영역 기준의 간격이다.
            public const float TitleH = 44f, TitleFont = 30f, TitleCloseGap = 16f;
            public const float DayPointsGap = 12f, DayPointsH = 28f, DayPointsFont = 20f;
            public const float CloseSize = 48f, CloseIconSize = 32f;
            public const float CardsGap = 16f, CardPitch = 108f, CardH = 96f;
            public const float CardIconX = 20f, CardIconY = 24f, CardIconSize = 48f;
            public const float CardLabelX = 92f, CardLabelY = 24f, CardLabelRightReserve = 252f, CardRowH = 48f, CardLabelFont = 26f;
            public const float CardValueRight = 20f, CardValueW = 132f, CardValueFont = 36f;
            public const float HintH = 28f, HintFont = 18f;
        }

        public const string RootId = "status", TitleId = "status.title", DayPointsId = "status.day-points", CloseId = "status.close", HintId = "status.close-hint";

        /// <summary>카드 셋의 순서: 체력, 집중, 스트레스. 아이콘 키와 이름 문구 키.</summary>
        private static readonly (string Id, string Icon, string LabelKey)[] Cards =
        {
            ("energy", "icon:energy", "ui.status.energy"),
            ("focus", "icon:focus", "ui.status.focus"),
            ("stress", "icon:stress", "ui.status.stress"),
        };

        private static readonly UiColor Ink = UnityLayout.Color(48 / 255f, 71 / 255f, 83 / 255f);

        private readonly UiLabel[] _labels = new UiLabel[Cards.Length];
        private readonly UiLabel[] _values = new UiLabel[Cards.Length];
        private UiLabel _title, _dayPoints, _hint;
        private StatusModel _model = StatusModel.Empty;

        public StatusScreen(ScreenServices services) : base(services) { }

        public StatusModel Model => _model;

        public static string CardId(int index) => "status.card." + Cards[index].Id;
        public static string ValueId(int index) => "status.value." + Cards[index].Id;
        public static string LabelId(int index) => "status.label." + Cards[index].Id;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Layout.Width, Layout.Height);
            UiInsets pad = Services.PaddingOf(UiSkin.Panel);
            float inner = Layout.Width - pad.Horizontal;
            float closeX = Layout.Width - pad.Right - Layout.CloseSize;

            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Layout.TitleFont, Color = Ink });
            _title.Place(0f, 0f, pad.Left, pad.Top, closeX - Layout.TitleCloseGap - pad.Left, Layout.TitleH);
            float dayY = pad.Top + Layout.TitleH + Layout.DayPointsGap;
            _dayPoints = root.Add(new UiLabel { Id = DayPointsId, FontSize = Layout.DayPointsFont, Color = Ink });
            _dayPoints.Place(0f, 0f, pad.Left, dayY, inner, Layout.DayPointsH);

            var close = root.Add(new UiButton { Id = CloseId, Skin = UiSkin.Slot });
            close.Place(0f, 0f, closeX, pad.Top, Layout.CloseSize, Layout.CloseSize);
            close.Clicked += Close;
            float inset = (Layout.CloseSize - Layout.CloseIconSize) / 2f;
            close.Add(new UiImage { Image = "icon:close" }).Place(0f, 0f, inset, inset, Layout.CloseIconSize, Layout.CloseIconSize);

            float cardTop = dayY + Layout.DayPointsH + Layout.CardsGap;
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = root.Add(new UiPanel { Id = CardId(i), Skin = UiSkin.Slot });
                card.Place(0f, 0f, pad.Left, cardTop + i * Layout.CardPitch, inner, Layout.CardH);
                card.Add(new UiImage { Image = Cards[i].Icon }).Place(0f, 0f, Layout.CardIconX, Layout.CardIconY, Layout.CardIconSize, Layout.CardIconSize);
                _labels[i] = card.Add(new UiLabel { Id = LabelId(i), FontSize = Layout.CardLabelFont, Color = Ink });
                _labels[i].Place(0f, 0f, Layout.CardLabelX, Layout.CardLabelY, inner - Layout.CardLabelRightReserve, Layout.CardRowH);
                _values[i] = card.Add(new UiLabel { Id = ValueId(i), FontSize = Layout.CardValueFont, Color = Ink, Align = UiAlign.Right });
                _values[i].Place(0f, 0f, inner - Layout.CardValueRight - Layout.CardValueW, Layout.CardLabelY, Layout.CardValueW, Layout.CardRowH);
            }

            _hint = root.Add(new UiLabel { Id = HintId, FontSize = Layout.HintFont, Color = Ink, Align = UiAlign.Right });
            _hint.Place(0f, 0f, pad.Left, Layout.Height - pad.Bottom - Layout.HintH, inner, Layout.HintH);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Student || topic == ChangeTopics.Day || topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void Refresh()
        {
            _model = StatusModel.From(Services.Session);
            _title.Text = T("ui.status.title");
            _hint.Text = T("ui.status.close-hint");
            for (int i = 0; i < Cards.Length; i++) _labels[i].Text = T(Cards[i].LabelKey);

            if (!_model.HasGame)
            {
                _dayPoints.Text = T("ui.status.waiting");
                foreach (UiLabel value in _values) value.Text = "--";
                return;
            }
            _dayPoints.Text = F("ui.status.day-time", _model.Day, _model.ClockMinutes / 60, _model.ClockMinutes % 60);
            _values[0].Text = _model.Energy.ToString();
            _values[1].Text = _model.Focus.ToString();
            _values[2].Text = _model.Stress.ToString();
        }
    }
}
