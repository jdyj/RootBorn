using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Interaction;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Dialogue
{
    /// <summary>하루 이벤트 화면을 여는 값: 이벤트 id와 그 이벤트를 만난 장소 id.</summary>
    public sealed record DailyEventOpen(string EventId, string LocationId);

    /// <summary>하루 이벤트 선택지 한 줄: id, 이름 키, 결과 요약 키.</summary>
    public readonly record struct DailyEventChoiceRow(string Id, string NameKey, string SummaryKey);

    /// <summary>
    /// 하루 이벤트 화면의 표시 모델(Unity DailyEventPanel.Show): 제목·설명 키, 기한 문구(정한 시각 / 미룰 수 있음 / 선택),
    /// 선택지(최대 3), 미루기 버튼이 보이는지. 이름 키는 Core 정의의 DisplayNameKey(비면 id — Unity와 같음).
    /// </summary>
    public sealed class DailyEventModel
    {
        public const int MaxChoices = 3;

        public string EventId { get; private set; }
        public string LocationId { get; private set; }
        public string TitleKey { get; private set; }
        public string DescriptionKey { get; private set; }
        /// <summary>기한 문구 키와 값(정한 시각이면 분). 키가 비면 기한 줄이 없다.</summary>
        public string DeadlineKey { get; private set; } = string.Empty;
        public int DeadlineMinutes { get; private set; }
        public DailyEventChoiceRow[] Choices { get; private set; } = Array.Empty<DailyEventChoiceRow>();
        public bool CanDefer { get; private set; }

        public static DailyEventModel From(DailyEvent dailyEvent, string locationId)
        {
            var model = new DailyEventModel { EventId = dailyEvent?.Id, LocationId = locationId };
            if (dailyEvent == null)
            {
                model.TitleKey = "life.event.ui.title";
                model.DescriptionKey = "life.event.ui.none";
                return model;
            }
            model.TitleKey = string.IsNullOrEmpty(dailyEvent.DisplayNameKey) ? "life.event.ui.title" : dailyEvent.DisplayNameKey;
            model.DescriptionKey = string.IsNullOrEmpty(dailyEvent.DescriptionKey) ? "life.event.ui.none" : dailyEvent.DescriptionKey;
            DailyEventKind kind = dailyEvent.Kind;
            if (kind != null)
            {
                if (kind.IsTimed && kind.DueTimeMinutes > 0) { model.DeadlineKey = "life.event.ui.due"; model.DeadlineMinutes = kind.DueTimeMinutes; }
                else model.DeadlineKey = kind.IsDeferrable ? "life.event.ui.deferrable" : "life.event.ui.optional";
                model.CanDefer = kind.IsDeferrable;
            }
            var choices = new List<DailyEventChoiceRow>(MaxChoices);
            for (int i = 0; i < dailyEvent.Choices.Count && i < MaxChoices; i++)
            {
                DailyEventChoice choice = dailyEvent.Choices[i];
                if (choice == null) continue;
                choices.Add(new DailyEventChoiceRow(choice.Id, choice.DisplayNameKey, choice.ResultSummaryKey));
            }
            model.Choices = choices.ToArray();
            return model;
        }
    }

    /// <summary>
    /// 하루 이벤트(Unity DailyEventPanel). 가운데 1120x800 패널: 제목, 설명, 기한, 선택지 버튼(이름과 결과 요약 두 줄), 결과 글,
    /// 미루기(미룰 수 있는 종류만)·지나가기 버튼, 오른쪽 위 닫기. 이동을 막는다(Unity: UiGameplayInputLock).
    /// 고르기·미루기·지나가기는 결과 글만 바꾸고 패널은 그대로 둔다(Unity 판과 같음). 닫기는 기록 없이 닫는다.
    /// 여는 때는 DailyEventTrigger가 정한다.
    /// </summary>
    public sealed class DailyEventScreen : GameScreen
    {
        /// <summary>배치 — Unity DailyEventPanel.BuildIfNeeded 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 1120f, Height = 800f;
            public const float TitleY = 320f, TitleW = 880f, TitleH = 48f, TitleFont = 28f;
            public const float DescriptionY = 250f, DescriptionW = 944f, DescriptionH = 72f, DescriptionFont = 20f;
            public const float DeadlineY = 195f, DeadlineW = 944f, DeadlineH = 32f, DeadlineFont = 18f;
            public const float ResultY = -222f, ResultW = 944f, ResultH = 96f, ResultFont = 20f;
            public const float ChoiceTopY = 122f, ChoiceStep = 108f, ChoiceW = 944f, ChoiceH = 92f, ChoiceFont = 20f;
            public const float CloseX = 504f, CloseY = 342f, CloseSize = 48f, CloseFont = 20f;
            public const float DeferX = -130f, DeclineX = 130f, BottomButtonY = -326f, BottomButtonW = 210f, BottomButtonH = 52f, BottomFont = 20f;
            public const string CloseGlyph = "×";
            public static readonly UiColor Ink = UnityLayout.Color(0.25f, 0.18f, 0.12f);
        }

        public const string PanelId = "daily-event";
        public const string TitleId = "daily-event.title";
        public const string DescriptionId = "daily-event.description";
        public const string DeadlineId = "daily-event.deadline";
        public const string ResultId = "daily-event.result";
        public const string CloseId = "daily-event.close";
        public const string DeferId = "daily-event.defer";
        public const string DeclineId = "daily-event.decline";
        public static string ChoiceId(int index) => "daily-event.choice." + index;

        private UiLabel _title, _deadline;
        private UiTextBlock _description, _result;
        private readonly UiMultilineButton[] _choices = new UiMultilineButton[DailyEventModel.MaxChoices];
        private UiButton _defer, _decline;
        private string _resultKey;
        private object[] _resultArgs;
        private string _resultExtraKey;

        public DailyEventScreen(ScreenServices services) : base(services) { }

        public DailyEventModel Model { get; private set; }
        public LifeActivityResult? LastResult { get; private set; }
        public DailyEventRecord LastRecord { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _description = root.Add(new UiTextBlock { Id = DescriptionId, FontSize = Metrics.DescriptionFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.DescriptionY, Metrics.DescriptionW, Metrics.DescriptionH);
            _deadline = root.Add(new UiLabel { Id = DeadlineId, FontSize = Metrics.DeadlineFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.DeadlineY, Metrics.DeadlineW, Metrics.DeadlineH);
            _result = root.Add(new UiTextBlock { Id = ResultId, FontSize = Metrics.ResultFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.ResultY, Metrics.ResultW, Metrics.ResultH);
            for (int i = 0; i < _choices.Length; i++)
            {
                int index = i;
                _choices[i] = root.Add(new UiMultilineButton { Id = ChoiceId(i), FontSize = Metrics.ChoiceFont })
                    .Centered(0f, Metrics.ChoiceTopY - i * Metrics.ChoiceStep, Metrics.ChoiceW, Metrics.ChoiceH);
                _choices[i].Clicked += () => Choose(index);
            }
            var close = root.Add(new UiButton { Id = CloseId, Text = Metrics.CloseGlyph, FontSize = Metrics.CloseFont })
                .Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseSize, Metrics.CloseSize);
            close.Clicked += Close;
            _defer = root.Add(new UiButton { Id = DeferId, FontSize = Metrics.BottomFont })
                .Centered(Metrics.DeferX, Metrics.BottomButtonY, Metrics.BottomButtonW, Metrics.BottomButtonH);
            _defer.Clicked += Defer;
            _decline = root.Add(new UiButton { Id = DeclineId, FontSize = Metrics.BottomFont })
                .Centered(Metrics.DeclineX, Metrics.BottomButtonY, Metrics.BottomButtonW, Metrics.BottomButtonH);
            _decline.Clicked += Decline;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            var open = context.Argument as DailyEventOpen;
            DailyEvent dailyEvent = null;
            Services.Session?.Definitions.TryGet(open?.EventId ?? string.Empty, out dailyEvent);
            Model = DailyEventModel.From(dailyEvent, open?.LocationId ?? context.Interaction?.TargetId);
            LastResult = null;
            LastRecord = null;
            SetResult(null);
            WriteAll();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language && Model != null) WriteAll();
        }

        private void WriteAll()
        {
            _title.Text = T(Model.TitleKey);
            _description.Text = T(Model.DescriptionKey);
            _deadline.Text = string.IsNullOrEmpty(Model.DeadlineKey) ? string.Empty
                : Model.DeadlineMinutes > 0 ? F(Model.DeadlineKey, Model.DeadlineMinutes) : T(Model.DeadlineKey);
            bool playable = Services.Session != null && Model.EventId != null;
            for (int i = 0; i < _choices.Length; i++)
            {
                bool used = i < Model.Choices.Length;
                _choices[i].Visible = used;
                _choices[i].Enabled = playable;
                if (used) _choices[i].Text = T(Model.Choices[i].NameKey) + "\n" + T(Model.Choices[i].SummaryKey);
            }
            _defer.Visible = Model.CanDefer;
            _defer.Enabled = playable;
            _defer.Text = T("life.event.ui.later");
            _decline.Enabled = playable;
            _decline.Text = T("life.event.ui.decline");
            WriteResult();
        }

        private void SetResult(string key, object[] args = null, string extraKey = null)
        {
            _resultKey = key; _resultArgs = args; _resultExtraKey = extraKey;
            WriteResult();
        }

        private void WriteResult()
        {
            if (_result == null) return;
            if (string.IsNullOrEmpty(_resultKey)) { _result.Text = string.Empty; return; }
            string text = _resultArgs != null ? F(_resultKey, _resultArgs) : T(_resultKey);
            if (!string.IsNullOrEmpty(_resultExtraKey)) text += "\n" + T(_resultExtraKey);
            _result.Text = text;
        }


        /// <summary>선택지 고르기(Unity: 요청 id = 이벤트 id + ":" + 선택지 id). 결과는 결과 글로.</summary>
        private void Choose(int index)
        {
            if (Services.Session == null || Model == null || index >= Model.Choices.Length) return;
            DailyEventChoiceRow row = Model.Choices[index];
            string eventId = Model.EventId, location = Model.LocationId;
            LifeActivityResult result = Services.Session.ChooseDailyEvent(eventId, row.Id, eventId + ":" + row.Id, location);
            LastResult = result;
            LastRecord = Services.Session.DailyEvents.GetRecord(eventId);
            if (result.Kind == LifeActivityResultKind.Applied) SetResult("life.event.ui.applied", null, row.SummaryKey);
            else SetResult(result.Kind == LifeActivityResultKind.DuplicateRequest ? "life.event.ui.duplicate" : "life.event.ui.unavailable");
        }

        /// <summary>미루기·지나가기(GameSession이 판정하고 저장한다). 적용되지 않으면 아무것도 바꾸지 않는다.</summary>
        private void Defer()
        {
            if (Services.Session == null || Model?.EventId == null) return;
            if (!Services.Session.DeferDailyEvent(Model.EventId, out DailyEventRecord record)) return;
            LastRecord = record;
            SetResult("life.event.ui.deferred", new object[] { record.DueDay });
        }

        private void Decline()
        {
            if (Services.Session == null || Model?.EventId == null) return;
            if (!Services.Session.DeclineDailyEvent(Model.EventId, out DailyEventRecord record)) return;
            LastRecord = record;
            SetResult("life.event.ui.declined");
        }
    }

    /// <summary>
    /// 하루 이벤트를 여는 때(Unity DailyEventRuntimeInstaller): 장소 지점에서 상호작용한 뒤, 그 상호작용이 연 화면(장소 화면)이
    /// 닫히면, 그 장소에서 지금 고를 수 있는 이벤트가 있으면 첫 것을 연다. 그 상호작용이 화면을 열지 않았으면 바로 본다.
    /// Unity 판은 맵에 들어갈 때가 아니라 이 때만 열었다. 어떤 상호작용 종류가 장소 지점인지는 등록할 때 넘긴다(엔티티 id가 아니다).
    /// </summary>
    public sealed class DailyEventTrigger
    {
        private readonly ScreenServices _services;
        private readonly HashSet<string> _kinds;
        private readonly string _screenId;
        private InteractionEvent? _pending;
        private GameScreen _waitFor;

        public DailyEventTrigger(ScreenServices services, string screenId, params string[] locationKinds)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _screenId = screenId;
            _kinds = new HashSet<string>(locationKinds ?? Array.Empty<string>(), StringComparer.Ordinal);
            _services.Screens.Interacted += OnInteracted;
            _services.Screens.Closed += OnClosed;
        }

        private void OnInteracted(InteractionEvent interaction, GameScreen opened)
        {
            if (!_kinds.Contains(interaction.Kind ?? string.Empty) || _services.Session == null) return;
            _pending = interaction;
            _waitFor = opened;
            if (opened == null || !opened.IsOpen) Present();
        }

        private void OnClosed(GameScreen screen)
        {
            if (_pending.HasValue && screen == _waitFor) Present();
        }

        private void Present()
        {
            InteractionEvent interaction = _pending.Value;
            _pending = null;
            _waitFor = null;
            DailyEvent[] available = _services.Session.AvailableDailyEvents(interaction.TargetId);
            if (available.Length == 0) return;
            _services.Screens.Open(_screenId, interaction, new DailyEventOpen(available[0].Id, interaction.TargetId));
        }
    }
}
