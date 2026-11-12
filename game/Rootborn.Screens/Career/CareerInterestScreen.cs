using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>장래희망 카드 한 장: 정의 id, 이름 키, 상태(Core 판정), 상태 문구 키, 지금 고른 희망인지.</summary>
    public readonly record struct CareerInterestCard(string InterestId, string NameKey, CareerInterestSelectionState State, string StateKey, bool IsCurrent);

    /// <summary>장래희망 상세: 이름·설명 키, 상태, 잠김 안내가 필요한지, 관련 장소·활동·다음에 해볼 일 키.</summary>
    public sealed class CareerInterestDetail
    {
        public CareerInterestDetail(CareerInterestCard card, string descriptionKey, string[] locationKeys, string[] activityKeys, string[] recommendedKeys)
        {
            Card = card; DescriptionKey = descriptionKey ?? string.Empty;
            LocationKeys = locationKeys ?? Array.Empty<string>(); ActivityKeys = activityKeys ?? Array.Empty<string>(); RecommendedKeys = recommendedKeys ?? Array.Empty<string>();
        }

        public CareerInterestCard Card { get; }
        public string DescriptionKey { get; }
        public string[] LocationKeys { get; }
        public string[] ActivityKeys { get; }
        public string[] RecommendedKeys { get; }
        public bool ShowLockedReason => Card.State == CareerInterestSelectionState.LockedByCondition;
    }

    /// <summary>
    /// 장래희망 화면의 표시 모델(Unity CareerInterestPanel + CareerInterestSummaryBuilder): 정의 순서의 카드들과 카드마다의 상세.
    /// 상태는 Core CareerInterest의 판정이다(해금 조건 → 하루 변경 제한). 화면에는 규칙이 없다.
    /// </summary>
    public sealed class CareerInterestModel
    {
        public static readonly CareerInterestModel Empty = new CareerInterestModel(Array.Empty<CareerInterestDetail>());

        public CareerInterestModel(CareerInterestDetail[] details)
        {
            Details = details;
            Cards = new CareerInterestCard[details.Length];
            for (int i = 0; i < details.Length; i++) Cards[i] = details[i].Card;
        }

        public CareerInterestCard[] Cards { get; }
        public CareerInterestDetail[] Details { get; }

        public static CareerInterestModel From(GameSession session)
        {
            if (session == null) return Empty;
            var details = new List<CareerInterestDetail>(8);
            // Unity 판은 registry에 등록된 장래희망만 보였다(registry의 careerInterests). 정의가 더 있어도(옛 관심사) 등록분만 보인다.
            IReadOnlyList<string> registered = session.Definitions.RegistryIds("careerInterests");
            foreach ((CareerInterest interest, CareerInterestSelectionEvaluation _) in session.CareerInterestChoices())
            {
                if (registered.Count > 0 && !Contains(registered, interest.Id)) continue;
                CareerInterestSummary summary = CareerInterestSummaryBuilder.Build(interest, session.CareerInterests, session.CareerCandidates, session.Student, session.StudentDay);
                bool current = session.CareerInterests.CurrentInterestId == interest.Id;
                var card = new CareerInterestCard(interest.Id, interest.DisplayNameKey, summary.State, StateKey(summary.State, current), current);
                details.Add(new CareerInterestDetail(card, summary.Description, summary.RelatedLocationKeys, summary.RelatedActivityKeys, summary.RecommendedActionKeys));
            }
            return new CareerInterestModel(details.ToArray());
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>상태 문구 키(Unity StateText). 고를 수 있는 희망은 '선택 가능', 지금 고른 희망만 '선택됨'.</summary>
        public static string StateKey(CareerInterestSelectionState state, bool current)
        {
            switch (state)
            {
                case CareerInterestSelectionState.Selected: return current ? "ui.interest-state.selected" : "ui.interest-state.available";
                case CareerInterestSelectionState.ChangePending: return "ui.interest-state.pending";
                case CareerInterestSelectionState.ChangedToday: return "ui.interest-state.changed";
                case CareerInterestSelectionState.LockedByCondition: return "ui.interest-state.locked";
                default: return "ui.interest-state.available";
            }
        }
    }

    /// <summary>
    /// 장래희망(Unity CareerInterestPanel). HUD 버튼(ui.career-interest.open)이 연다. 가운데 1440x840 패널: 제목, 가로 스크롤 카드 줄(카드 300x300),
    /// 고른 카드의 상세(상태, 설명, 관련 장소·활동, 다음에 해볼 일 — 넘치면 휠로 스크롤), 닫기.
    /// 카드를 누르면 고르기를 시도하고(GameSession.SelectCareerInterest — 해금·하루 변경 제한은 Core가 판정) 상세는 그 카드를 보인다.
    /// 이동을 막는다(Unity: UiGameplayInputLock). 열 때는 첫 카드의 상세를 보인다.
    /// </summary>
    public sealed class CareerInterestScreen : CareerScreenBase
    {
        /// <summary>배치 — Unity CareerInterestPanel.BuildIfNeeded 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 1440f, Height = 840f;
            public const float TitleX = -350f, TitleY = 350f, TitleW = 640f, TitleH = 44f, TitleFont = 32f;
            public const float CardsY = 105f, CardsW = 1344f, CardsH = 314f;
            public const float CardW = 300f, CardSpacing = 16f;
            public const float IconSize = 144f, IconTop = 28f;
            public const float LabelTop = 198f, LabelW = 264f, LabelH = 76f, LabelFont = 20f;
            public const float DetailsY = -220f, DetailsW = 1344f, DetailsH = 266f, DetailsFont = 20f;
            public const float DetailsPadX = 20f, DetailsPadTop = 10f;
            public const float CloseX = 600f, CloseY = 350f, CloseW = 140f, CloseH = 58f, CloseFont = 20f;
            /// <summary>카드 아이콘의 키(Unity: 정의별 아이콘이 없으면 "journal"). 정의별 아이콘 대응은 테마 데이터의 몫이다.</summary>
            public const string IconKey = "icon:journal";
            public static readonly UiColor Ink = UnityLayout.Color(48f / 255f, 71f / 255f, 83f / 255f);
            public static readonly UiColor CurrentTint = UnityLayout.Color(1f, 0.97f, 0.86f);
        }

        public const string PanelId = "career-interest";
        public const string TitleId = "career-interest.title";
        public const string CardsId = "career-interest.cards";
        public const string DetailsId = "career-interest.details";
        public const string CloseId = "career-interest.close";

        private UiLabel _title;
        private UiCardRow _cards;
        private UiScrollText _details;
        private UiButton _close;
        private readonly CardLabel _cardLabel = new CardLabel();
        private string[] _cardTexts = Array.Empty<string>();

        public CareerInterestScreen(ScreenServices services) : base(services) { }

        public CareerInterestModel Model { get; private set; } = CareerInterestModel.Empty;
        /// <summary>상세에 보이는 카드 번호(-1이면 없음).</summary>
        public int DetailIndex { get; private set; } = -1;
        /// <summary>마지막으로 카드를 눌러 고르기를 시도한 결과.</summary>
        public CareerInterestSelectionResult? LastResult { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink });
            _title.Centered(Metrics.TitleX, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _cards = root.Add(new UiCardRow
            {
                Id = CardsId, CardWidth = Metrics.CardW, Spacing = Metrics.CardSpacing, Padding = 0f, DrawBackground = false,
            });
            _cards.Centered(0f, Metrics.CardsY, Metrics.CardsW, Metrics.CardsH);
            _cards.DrawCard = PaintCard;
            _cards.CardClicked += SelectAndShow;
            _details = root.Add(new UiScrollText { Id = DetailsId, FontSize = Metrics.DetailsFont, Color = Metrics.Ink, PaddingX = Metrics.DetailsPadX, PaddingTop = Metrics.DetailsPadTop });
            _details.Centered(0f, Metrics.DetailsY, Metrics.DetailsW, Metrics.DetailsH);
            _close = root.Add(new UiButton { Id = CloseId, FontSize = Metrics.CloseFont });
            _close.Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseW, Metrics.CloseH);
            _close.Clicked += Close;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            LastResult = null;
            Model = CareerInterestModel.From(Services.Session);
            DetailIndex = Model.Cards.Length > 0 ? 0 : -1;
            Show();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic != ChangeTopics.Career && topic != ChangeTopics.Day && topic != ChangeTopics.Student && topic != ChangeTopics.Language && topic != ChangeTopics.All) return;
            Model = CareerInterestModel.From(Services.Session);
            if (DetailIndex >= Model.Cards.Length) DetailIndex = Model.Cards.Length > 0 ? 0 : -1;
            Show();
        }

        private void SelectAndShow(int index)
        {
            if (index < 0 || index >= Model.Cards.Length || Services.Session == null) return;
            DetailIndex = index;
            string id = Model.Cards[index].InterestId;
            // Unity 요청 id와 같은 모양: 같은 날 같은 희망을 다시 눌러도 같은 요청이다.
            string requestId = "career-interest-ui-" + id + "-" + Services.Session.StudentDay;
            CareerInterestSelectionResult result = Services.Session.SelectCareerInterest(id, requestId);
            LastResult = result;
            Model = CareerInterestModel.From(Services.Session);
            Show();
        }

        private void Show()
        {
            _title.Text = T("ui.career-interest.title");
            _close.Text = T("facility.close");
            _cards.CardCount = Model.Cards.Length;
            _cardTexts = new string[Model.Cards.Length]; // 카드 글자는 값이 바뀔 때만 만든다(그릴 때마다 만들지 않는다)
            for (int i = 0; i < _cardTexts.Length; i++) _cardTexts[i] = Shown(Model.Cards[i].NameKey) + "\n" + T(Model.Cards[i].StateKey);
            _details.Text = DetailText();
        }

        private string DetailText()
        {
            if (DetailIndex < 0 || DetailIndex >= Model.Details.Length) return T("ui.career-interest.empty");
            CareerInterestDetail d = Model.Details[DetailIndex];
            var lines = new List<string>(16) { Shown(d.Card.NameKey), F("ui.career-interest.state-line", T(d.Card.StateKey)) };
            if (d.ShowLockedReason) lines.Add(T("dream.ui.locked-reason"));
            if (!string.IsNullOrEmpty(d.DescriptionKey)) lines.Add(Shown(d.DescriptionKey));
            AddSection(lines, "ui.career-interest.locations", d.LocationKeys, "ui.career-interest.no-locations");
            AddSection(lines, "ui.career-interest.activities", d.ActivityKeys, "ui.career-interest.no-activities");
            AddSection(lines, "ui.career-interest.recommended", d.RecommendedKeys, "ui.career-interest.explore");
            return JoinLines(lines);
        }

        private void AddSection(List<string> lines, string titleKey, string[] keys, string emptyKey)
        {
            lines.Add(T(titleKey));
            bool any = false;
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                lines.Add(Shown(key));
                any = true;
            }
            if (!any) lines.Add(T(emptyKey));
        }

        private void PaintCard(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (index >= Model.Cards.Length || index >= _cardTexts.Length) return;
            CareerInterestCard card = Model.Cards[index];
            UiColor tint = card.IsCurrent ? Metrics.CurrentTint : state.Hovered ? UiTheme.ButtonHovered : UiTheme.White;
            list.Panel(rect, UiSkin.Slot, tint);
            var icon = new UiRect(rect.CenterX - Metrics.IconSize / 2f, rect.Y + Metrics.IconTop, Metrics.IconSize, Metrics.IconSize);
            list.Image(icon, Metrics.IconKey, default, UiTheme.White);
            var label = new UiRect(rect.CenterX - Metrics.LabelW / 2f, rect.Y + Metrics.LabelTop, Metrics.LabelW, Metrics.LabelH);
            _cardLabel.Draw(list, text, label, Shown(card.NameKey) + "\n" + T(card.StateKey), Metrics.LabelFont, Metrics.Ink);
        }
    }
}
