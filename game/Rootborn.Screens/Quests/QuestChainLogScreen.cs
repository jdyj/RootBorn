using System;
using System.Collections.Generic;
using Rootborn.Core.Quests;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Objectives.Sources;
using Rootborn.Ui;

namespace Rootborn.Screens.Quests
{
    /// <summary>퀘스트 체인 기록의 줄 하나: 체인 id, 이름 키, 상태.</summary>
    public readonly record struct QuestChainRow(string ChainId, string NameKey, QuestChainState State);

    /// <summary>
    /// 퀘스트 체인 기록의 표시 모델(Unity QuestChainLogPanel.Refresh): 진로·상태 거르기, 보이는 체인(최대 8), 고른 체인의 상세와
    /// 받기·추적·보상 단추를 누를 수 있는지. 개발용 체인(DeveloperOnly)은 넣지 않는다. 거르기는 Unity처럼 화면 안에서 고를 수단이 없어 "전체"다.
    /// </summary>
    public sealed class QuestChainLogModel
    {
        public const int MaxCards = 8;
        public const string CompletionRewardId = "completion";

        public string CareerFilter { get; private set; } = string.Empty;
        public QuestChainState? StateFilter { get; private set; }
        public QuestChainRow[] Rows { get; private set; } = Array.Empty<QuestChainRow>();
        public int Selected { get; private set; } = -1;
        public string SelectedChainId => Selected >= 0 ? Rows[Selected].ChainId : string.Empty;
        public QuestChainState SelectedState => Selected >= 0 ? Rows[Selected].State : QuestChainState.Available;
        /// <summary>상세: 지금 단계 첫 목표(이름 키, 진행, 필요), 막힘 이유·요약, 보상을 받았는지.</summary>
        public string ObjectiveKey { get; private set; } = string.Empty;
        public int ObjectiveCurrent { get; private set; }
        public int ObjectiveRequired { get; private set; }
        public string BlockedReason { get; private set; } = string.Empty;
        public string BlockedSummary { get; private set; } = string.Empty;
        public bool RewardClaimed { get; private set; }
        public bool CanAccept { get; private set; }
        public bool CanTrack { get; private set; }
        public bool CanClaim { get; private set; }

        public static QuestChainLogModel From(Rootborn.Game.GameSession session, string selectedChainId, string careerFilter, QuestChainState? stateFilter)
        {
            var model = new QuestChainLogModel { CareerFilter = careerFilter ?? string.Empty, StateFilter = stateFilter };
            if (session == null) return model;
            QuestChainLog log = session.QuestChains;
            var rows = new List<QuestChainRow>(MaxCards);
            foreach (QuestChainDefinition chain in session.Definitions.All<QuestChainDefinition>())
            {
                if (chain == null || chain.DeveloperOnly) continue; // 개발용 체인은 목표 수첩(QuestChainSource)과 같이 보이지 않는다
                QuestChainState state = log.GetState(chain);
                if (!string.IsNullOrEmpty(model.CareerFilter) && chain.RelatedCareerInterestId != model.CareerFilter) continue;
                if (stateFilter.HasValue && state != stateFilter.Value) continue;
                rows.Add(new QuestChainRow(chain.Id, chain.DisplayNameKey, state));
            }
            model.Rows = rows.ToArray();
            if (model.Rows.Length == 0) return model;
            model.Selected = Math.Max(0, Array.FindIndex(model.Rows, r => r.ChainId == selectedChainId));
            QuestChainDefinition selected = session.Chain(model.SelectedChainId);
            QuestStepDefinition step = log.GetCurrentStep(selected);
            QuestObjective first = step != null && step.Objectives.Count > 0 ? step.Objectives[0] : null;
            if (first != null) { model.ObjectiveKey = first.DisplayKey ?? string.Empty; model.ObjectiveCurrent = log.GetObjectiveProgress(selected, 0); model.ObjectiveRequired = first.RequiredCount; }
            QuestChainState s = model.SelectedState;
            if (s == QuestChainState.Blocked) { model.BlockedReason = log.GetBlockedReasonId(selected) ?? string.Empty; model.BlockedSummary = log.GetBlockedSummary(selected) ?? string.Empty; }
            model.RewardClaimed = log.IsRewardClaimed(selected, CompletionRewardId);
            // Unity는 시작 조건(EvaluateAvailability)과 보상 공간(CanClaimCompletionRewards)까지 미리 봤다. GameSession에 그 조회가 없어
            // 상태로만 정하고, 실제 판단은 명령이 한다(보고서 "필요한 변경").
            model.CanAccept = s == QuestChainState.Available;
            model.CanTrack = s == QuestChainState.Active || s == QuestChainState.Paused || s == QuestChainState.Tracked;
            model.CanClaim = s == QuestChainState.Completed && !model.RewardClaimed;
            return model;
        }
    }

    /// <summary>체인 카드: 두 줄(이름, 상태), 고른 카드는 테두리. 이 묶음의 새 부품.</summary>
    public sealed class QuestChainCard : UiNode
    {
        public string Title, Subtitle;
        public bool Selected;
        public float FontSize = 14f;
        public UiColor Ink = UiTheme.Ink;

        public QuestChainCard() { Interactive = true; Focusable = true; }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            UiColor tint = !IsActive ? UiTheme.ButtonDisabled : Selected || Hovered ? UiTheme.ButtonHovered : Pressed ? UiTheme.ButtonPressed : UiTheme.ButtonNormal;
            list.Panel(Rect, UiSkin.Button, tint);
            list.PushClip(Rect.Inset(4f));
            float line = text.LineHeight(FontSize), y = Rect.Y + (Rect.H - line * 2f - 2f) / 2f;
            if (!string.IsNullOrEmpty(Title)) list.Text(Rect.X + (Rect.W - text.Width(Title, FontSize)) / 2f, y, Title, FontSize, Ink, UiSkin.Button);
            if (!string.IsNullOrEmpty(Subtitle)) list.Text(Rect.X + (Rect.W - text.Width(Subtitle, FontSize)) / 2f, y + line + 2f, Subtitle, FontSize, Ink, UiSkin.Button);
            list.PopClip();
            if (Selected) list.Outline(Rect.Inset(3f), 2f, Ink);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>
    /// 퀘스트 체인 기록(Unity QuestChainLogPanel): 가운데 760x420 패널, 왼쪽 위 진로·상태 거르기, 가운데 체인 카드(최대 8),
    /// 오른쪽 상세, 아래 받기·추적·보상 단추. Unity 판에서는 이 패널을 여는 키나 상호작용이 없었다(설치만 하고 숨겨 두고 테스트만 열었다).
    /// 그래서 여기서도 등록만 하고 여는 길은 두지 않는다(ScreenStack.Open(id)로만 연다). 이동을 막지 않는다.
    /// </summary>
    public sealed class QuestChainLogScreen : GameScreen
    {
        /// <summary>배치 — Unity QuestChainLogPanel.BuildIfNeeded 수치(가운데 기준, y는 위로, 글자는 왼쪽 위 기준점). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 760f, Height = 420f;
            // 거르기 두 줄: 안쪽(테마 여백 — PanelArea) 왼쪽 위에서 아래로 StateGap 간격.
            public const float StateGap = 35f, FilterW = 180f, FilterH = 28f, FilterFont = 15f;
            // 상세: 가운데 기준 x=DetailX에서 안쪽 오른쪽 끝까지, 안쪽 위에서 DetailGap 아래부터 단추 줄 위 DetailBottomGap까지.
            // Unity 상세는 폭 320이라 패널(반폭 380) 밖으로 110 나갔다. 안쪽 영역 안으로 줄였다(달라진 동작).
            public const float DetailX = 170f, DetailGap = 20f, DetailBottomGap = 8f, DetailFont = 14f;
            public const float CardX = -120f, CardTopY = 130f, CardStep = 40f, CardW = 260f, CardH = 36f, CardFont = 14f;
            public const float ButtonY = -160f, AcceptX = 70f, TrackX = 190f, ClaimX = 310f, ButtonW = 105f, ButtonH = 38f, ButtonFont = 14f;
            public static readonly UiColor Ink = UnityLayout.Color(.25f, .18f, .12f);
        }

        public const string PanelId = "quest-chain-log";
        public const string CareerId = "quest-chain-log.career";
        public const string StateId = "quest-chain-log.state";
        public const string DetailId = "quest-chain-log.detail";
        public const string AcceptId = "quest-chain-log.accept";
        public const string TrackId = "quest-chain-log.track";
        public const string ClaimId = "quest-chain-log.claim";
        public static string CardId(int index) => "quest-chain-log.card." + index;

        private readonly QuestChainCard[] _cards = new QuestChainCard[QuestChainLogModel.MaxCards];
        private UiLabel _career, _state;
        private UiTextBlock _detail;
        private UiButton _accept, _track, _claim;
        private string _selectedChainId = string.Empty;

        public QuestChainLogScreen(ScreenServices services) : base(services) { }

        public QuestChainLogModel Model { get; private set; } = new QuestChainLogModel();

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            var area = new PanelArea(Metrics.Width, Metrics.Height, Services.PaddingOf(UiSkin.Panel));
            _career = root.Add(new UiLabel { Id = CareerId, FontSize = Metrics.FilterFont, Color = Metrics.Ink });
            _career.Place(0f, 0f, area.Left, area.Top, Metrics.FilterW, Metrics.FilterH);
            _state = root.Add(new UiLabel { Id = StateId, FontSize = Metrics.FilterFont, Color = Metrics.Ink });
            _state.Place(0f, 0f, area.Left, area.Top + Metrics.StateGap, Metrics.FilterW, Metrics.FilterH);
            float detailX = Metrics.Width / 2f + Metrics.DetailX, detailY = area.Top + Metrics.DetailGap;
            float buttonsTop = Metrics.Height / 2f - Metrics.ButtonY - Metrics.ButtonH / 2f;
            _detail = root.Add(new UiTextBlock { Id = DetailId, FontSize = Metrics.DetailFont, Color = Metrics.Ink, LineSpacing = 2 });
            _detail.Place(0f, 0f, detailX, detailY, area.Right - detailX, buttonsTop - Metrics.DetailBottomGap - detailY);
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = i;
                _cards[i] = root.Add(new QuestChainCard { Id = CardId(i), FontSize = Metrics.CardFont, Ink = Metrics.Ink, Visible = false })
                    .Centered(Metrics.CardX, Metrics.CardTopY - i * Metrics.CardStep, Metrics.CardW, Metrics.CardH);
                _cards[i].Clicked += () => Select(index);
            }
            _accept = Button(root, AcceptId, Metrics.AcceptX, Accept);
            _track = Button(root, TrackId, Metrics.TrackX, Track);
            _claim = Button(root, ClaimId, Metrics.ClaimX, Claim);
            return root;
        }

        private UiButton Button(UiNode root, string id, float x, Action click)
        {
            UiButton button = root.Add(new UiButton { Id = id, FontSize = Metrics.ButtonFont }).Centered(x, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            button.Clicked += click;
            return button;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

        protected internal override void OnChanged(string topic)
        {
            if (topic != ChangeTopics.Map) Refresh();
        }

        private void Refresh()
        {
            Model = QuestChainLogModel.From(Services.Session, _selectedChainId, Model.CareerFilter, Model.StateFilter);
            _selectedChainId = Model.SelectedChainId;
            string all = T("ui.quest-chain-log.all");
            _career.Text = F("ui.quest-chain-log.career-filter", string.IsNullOrEmpty(Model.CareerFilter) ? all : Model.CareerFilter);
            _state.Text = F("ui.quest-chain-log.state-filter", Model.StateFilter.HasValue ? StateName(Model.StateFilter.Value) : all);
            for (int i = 0; i < _cards.Length; i++)
            {
                bool used = i < Model.Rows.Length;
                _cards[i].Visible = used;
                if (!used) continue;
                _cards[i].Title = Shown(Model.Rows[i].NameKey);
                _cards[i].Subtitle = StateName(Model.Rows[i].State);
                _cards[i].Selected = i == Model.Selected;
            }
            _detail.Text = Detail();
            _accept.Text = T("ui.quest-chain-log.accept");
            _track.Text = T("ui.quest-chain-log.track");
            _claim.Text = T("ui.quest-chain-log.claim");
            _accept.Enabled = Services.Session != null && Model.CanAccept;
            _track.Enabled = Services.Session != null && Model.CanTrack;
            _claim.Enabled = Services.Session != null && Model.CanClaim;
        }

        private string StateName(QuestChainState state) => T("ui.journal.state." + state);

        private string Detail()
        {
            if (Model.Selected < 0) return string.Empty;
            var lines = new List<string>(5) { Shown(Model.Rows[Model.Selected].NameKey), StateName(Model.SelectedState) };
            if (!string.IsNullOrEmpty(Model.ObjectiveKey) || Model.ObjectiveRequired > 0)
                lines.Add(F("ui.journal.objective-line", Shown(Model.ObjectiveKey), Model.ObjectiveCurrent, Model.ObjectiveRequired));
            if (Model.SelectedState == QuestChainState.Blocked)
            {
                if (!string.IsNullOrEmpty(Model.BlockedReason)) lines.Add(Shown(Model.BlockedReason));
                if (!string.IsNullOrEmpty(Model.BlockedSummary)) lines.Add(Shown(Model.BlockedSummary));
            }
            if (Model.SelectedState == QuestChainState.Completed)
                lines.Add(F("ui.quest-chain-log.reward", T(Model.RewardClaimed ? "ui.quest-chain-log.reward-claimed" : "ui.quest-chain-log.reward-ready")));
            return string.Join("\n", lines);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= Model.Rows.Length) return;
            _selectedChainId = Model.Rows[index].ChainId;
            Refresh();
        }

        private void Accept()
        {
            string id = Model.SelectedChainId;
            if (Model.CanAccept) Services.Session.AcceptQuestChain(id);
        }

        /// <summary>
        /// 추적: 목표 수첩의 추적과 같은 길(GameSession.TrackObjective, 바로 저장). 추적 목표 HUD가 이 체인을 보인다.
        /// Unity QuestChainLogPanel은 체인 상태를 Tracked로 바꿨다 — GameSession에 그 명령이 없어 하지 않는다(보고서 "필요한 변경").
        /// </summary>
        private void Track()
        {
            string id = Model.SelectedChainId;
            if (!Model.CanTrack) return;
            Services.Session.TrackObjective(JournalCategories.Quests, QuestChainSource.StableKeyFor(id));
        }

        private void Claim()
        {
            string id = Model.SelectedChainId;
            if (Model.CanClaim) Services.Session.ClaimQuestChainRewards(id);
        }

        /// <summary>진로·상태 거르기(Unity SelectCareerFilter·SelectStateFilter·ClearStateFilter). Unity 판에도 화면 안의 조작은 없었다.</summary>
        public void SetFilters(string careerInterestId, QuestChainState? state)
        {
            Model = QuestChainLogModel.From(Services.Session, _selectedChainId, careerInterestId, state);
            Refresh();
        }
    }
}
