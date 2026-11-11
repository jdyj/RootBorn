using System;
using System.Text;
using Rootborn.Core.Quests;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>힌트 줄이 무엇인가: 문구 키, "다음: {목표}" 형식, 또는 데이터가 준 값(키일 수도 글일 수도 있다).</summary>
    public enum TrackedHintKind { None, Key, NextObjective, DataText }

    /// <summary>
    /// 추적 목표 HUD의 표시 모델: 추적 중인 체인 하나의 지금 단계(GameSession.TrackedObjective). 제목·목표 줄·힌트는 Unity
    /// QuestChainObjectiveSource가 수첩 줄을 만들던 순서 그대로: 제목은 단계 이름(없으면 체인 이름), 목표는 "목표 진행 / 필요" 줄을 " | "로 잇고,
    /// 힌트는 완료 → 막힘 요약 → 목표를 다 채우고 기다리는 조건 → 첫 목표("다음: ...") → 수첩을 보라는 문구.
    /// </summary>
    public sealed class TrackedObjectiveModel
    {
        public static readonly TrackedObjectiveModel None = new TrackedObjectiveModel();

        public bool HasChain { get; private set; }
        public string ChainId { get; private set; } = string.Empty;
        public string StepId { get; private set; } = string.Empty;
        public string TitleKey { get; private set; } = string.Empty;
        public QuestChainState State { get; private set; }
        public ObjectiveLine[] Objectives { get; private set; } = Array.Empty<ObjectiveLine>();
        public TrackedHintKind HintKind { get; private set; }
        /// <summary>HintKind에 따라: 문구 키, 다음 목표의 표시 키, 데이터 값.</summary>
        public string Hint { get; private set; } = string.Empty;

        public const string GenericObjectiveKey = "ui.journal.objective.generic";

        public static TrackedObjectiveModel From(GameSession session)
        {
            if (session == null) return None;
            TrackedObjectiveView view = session.TrackedObjective();
            if (!view.HasChain) return None;
            QuestChainDefinition chain = session.Chain(view.ChainId);
            var model = new TrackedObjectiveModel
            {
                HasChain = true,
                ChainId = view.ChainId,
                StepId = view.StepId,
                TitleKey = string.IsNullOrEmpty(view.StepNameKey) ? chain?.DisplayNameKey ?? string.Empty : view.StepNameKey,
                State = view.State,
                Objectives = view.Objectives,
            };

            QuestStepDefinition step = chain != null ? session.QuestChains.GetCurrentStep(chain) : null;
            if (view.State == QuestChainState.Completed) model.SetHint(TrackedHintKind.Key, "ui.journal.chain.completed-hint");
            else if (view.State == QuestChainState.Blocked)
            {
                if (string.IsNullOrEmpty(view.BlockedSummary)) model.SetHint(TrackedHintKind.Key, "ui.journal.chain.blocked-hint");
                else model.SetHint(TrackedHintKind.DataText, view.BlockedSummary);
            }
            else if (step != null && step.CompletionConditions.Count > 0 && session.QuestChains.AreCurrentObjectivesComplete(chain) && step.CompletionConditions[0] != null)
                model.SetHint(TrackedHintKind.DataText, step.CompletionConditions[0].BlockedSummary);
            else if (view.Objectives.Length > 0)
                model.SetHint(TrackedHintKind.NextObjective, string.IsNullOrEmpty(view.Objectives[0].DisplayKey) ? GenericObjectiveKey : view.Objectives[0].DisplayKey);
            else model.SetHint(TrackedHintKind.Key, "ui.journal.chain.open-hint");
            return model;
        }

        private void SetHint(TrackedHintKind kind, string value) { HintKind = kind; Hint = value ?? string.Empty; }

        /// <summary>바뀐 것이 없는지(같은 단계, 같은 진행, 같은 힌트).</summary>
        public bool SameAs(TrackedObjectiveModel other)
        {
            if (other == null || HasChain != other.HasChain || ChainId != other.ChainId || StepId != other.StepId || State != other.State
                || HintKind != other.HintKind || Hint != other.Hint || Objectives.Length != other.Objectives.Length) return false;
            for (int i = 0; i < Objectives.Length; i++)
                if (!Objectives[i].Equals(other.Objectives[i])) return false;
            return true;
        }
    }

    /// <summary>
    /// 추적 목표 HUD(Unity TrackedObjectiveHud): 위쪽 줄 아래 오른쪽의 640x200 칸, 수첩 아이콘과 제목·진행·힌트 세 줄.
    /// 추적 중인 체인이 없으면 보이지 않는다(Unity 판도 추적 대상이 없으면 숨겼다). 이동을 막는 화면이 떠 있는 동안도 숨긴다
    /// (Unity: 입력 잠금 중 투명). 상시 HUD는 이것 하나다(목표 수첩 규칙).
    /// </summary>
    public sealed class TrackedObjectiveHudScreen : GameScreen
    {
        /// <summary>배치(칸 안, 왼쪽 위 기준). Unity: 아이콘 (40, 52) 80x80 / 글자 x 136, 오른쪽 36 안쪽: 제목 y 36 높이 32 22, 진행 y 76 높이 28 18, 힌트 y 110 높이 54 18.</summary>
        private static class Layout
        {
            public const float IconX = 40f, IconY = 52f, IconSize = 80f;
            public const float TextX = 136f, TextRight = 36f;
            public const float TitleY = 36f, TitleHeight = 32f, TitleFont = 22f;
            public const float ProgressY = 76f, ProgressHeight = 28f, ProgressFont = 18f;
            public const float HintY = 110f, HintHeight = 54f, HintFont = 18f;
            public const string Icon = "icon:journal";
            public const string ObjectiveSeparator = " | ";
        }

        public const string PanelId = "hud.tracked";
        public const string TitleId = "hud.tracked.title";
        public const string ProgressId = "hud.tracked.progress";
        public const string HintId = "hud.tracked.hint";

        private UiPanel _panel;
        private UiLabel _title, _progress;
        private UiTextBlock _hint;
        private string _shownLanguage;
        private bool _subscribed;

        public TrackedObjectiveHudScreen(ScreenServices services) : base(services) { }

        public TrackedObjectiveModel Model { get; private set; } = TrackedObjectiveModel.None;
        public bool IsShown => _panel != null && _panel.Visible;
        /// <summary>보이는 세 줄(제목, 진행, 힌트).</summary>
        public string VisibleText => IsShown ? (_title.Text + "\n" + _progress.Text + "\n" + _hint.Text).Trim() : string.Empty;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.tracked.root" };
            _panel = HudLayout.PlaceTracked(root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel, Interactive = false }));
            _panel.Add(new UiImage { Image = Layout.Icon }).Place(0f, 0f, Layout.IconX, Layout.IconY, Layout.IconSize, Layout.IconSize);
            _title = Line(new UiLabel { Id = TitleId, FontSize = Layout.TitleFont, Color = HudLayout.Ink }, Layout.TitleY, Layout.TitleHeight);
            _progress = Line(new UiLabel { Id = ProgressId, FontSize = Layout.ProgressFont, Color = HudLayout.Ink }, Layout.ProgressY, Layout.ProgressHeight);
            _hint = Line(new UiTextBlock { Id = HintId, FontSize = Layout.HintFont, Color = HudLayout.Ink, LineSpacing = 2 }, Layout.HintY, Layout.HintHeight);
            _panel.Visible = false;
            return root;
        }

        private T Line<T>(T node, float y, float height) where T : UiNode
        {
            _panel.Add(node);
            node.AnchorMinX = 0f; node.AnchorMaxX = 1f; node.AnchorMinY = node.AnchorMaxY = 0f;
            node.OffsetMinX = Layout.TextX; node.OffsetMaxX = -Layout.TextRight;
            node.OffsetMinY = y; node.OffsetMaxY = y + height;
            return node;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!_subscribed && Services.Screens != null)
            {
                Services.Screens.Opened += _ => UpdateVisibility();
                Services.Screens.Closed += _ => UpdateVisibility();
                _subscribed = true;
            }
            Refresh(true);
        }

        protected internal override void OnChanged(string topic) => Refresh(false);

        private void Refresh(bool force)
        {
            TrackedObjectiveModel model = TrackedObjectiveModel.From(Services.Session);
            bool language = _shownLanguage != Services.Text.Language;
            if (force || language || !model.SameAs(Model))
            {
                Model = model;
                _shownLanguage = Services.Text.Language;
                if (model.HasChain) WriteText(model);
            }
            UpdateVisibility();
        }

        private void WriteText(TrackedObjectiveModel model)
        {
            _title.Text = string.IsNullOrEmpty(model.TitleKey) ? T("ui.objectives.tracked") : T(model.TitleKey);
            if (model.Objectives.Length == 0) _progress.Text = T("ui.journal.state." + model.State);
            else
            {
                var line = new StringBuilder(64);
                foreach (ObjectiveLine objective in model.Objectives)
                {
                    if (line.Length > 0) line.Append(Layout.ObjectiveSeparator);
                    line.Append(ObjectiveLabel(objective.DisplayKey)).Append(' ').Append(objective.Current).Append(" / ").Append(objective.Required);
                }
                _progress.Text = line.ToString();
            }
            switch (model.HintKind)
            {
                case TrackedHintKind.Key: _hint.Text = T(model.Hint); break;
                case TrackedHintKind.NextObjective: _hint.Text = F("ui.journal.next", ObjectiveLabel(model.Hint)); break;
                case TrackedHintKind.DataText: _hint.Text = Shown(model.Hint); break;
                default: _hint.Text = string.Empty; break;
            }
        }

        private string ObjectiveLabel(string displayKey) => T(string.IsNullOrEmpty(displayKey) ? TrackedObjectiveModel.GenericObjectiveKey : displayKey);

        private void UpdateVisibility()
        {
            if (_panel == null) return;
            bool blocked = false;
            if (Services.Screens != null)
                foreach (GameScreen screen in Services.Screens.OpenScreens)
                    if (screen.BlocksMovement) { blocked = true; break; }
            _panel.Visible = IsOpen && Model.HasChain && !blocked;
        }
    }
}
