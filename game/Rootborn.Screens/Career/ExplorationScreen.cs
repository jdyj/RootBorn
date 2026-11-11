using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>결과 한 줄에 쓰는 효과: 문구 키, 이름 키(물건 효과일 때만), 수. 부호가 있는 수(체력 +3)는 Signed.</summary>
    public readonly record struct ExplorationEffect(string Key, string NameKey, int Number, bool Signed);

    /// <summary>
    /// 탐색 선택 결과의 표시 모델(Unity ExplorationTextPresentation.Result): 결과 종류 문구 키, 조건 실패면 잠긴 이유 키,
    /// 얻은 것 줄들(물건·단서·도감·진로 힌트·후속 퀘스트·성장·그 밖의 변화)과 상태 줄들(체력·집중·스트레스·그 밖의 상태),
    /// 줄이 넘칠 때 쓰는 요약 개수. 적용되지 않은 결과는 효과를 보이지 않는다(잘못된 보상이 적용된 것처럼 보이면 안 된다).
    /// </summary>
    public sealed class ExplorationResultModel
    {
        public string StatusKey { get; private set; }
        /// <summary>조건 실패일 때만: 잠긴 이유 키(비면 일반 안내).</summary>
        public bool ShowsLockedReason { get; private set; }
        public string LockedReasonKey { get; private set; }
        public List<ExplorationEffect> Rewards { get; } = new List<ExplorationEffect>(8);
        public List<ExplorationEffect> Vitals { get; } = new List<ExplorationEffect>(4);
        public int RewardFacts { get; private set; }
        public int VitalFacts { get; private set; }

        /// <param name="itemNameKey">물건 id → 이름 키(찾지 못하거나 같은 id가 여럿이면 null).</param>
        public static ExplorationResultModel From(ExplorationChoiceResult result, Func<string, string> itemNameKey)
        {
            var model = new ExplorationResultModel { StatusKey = "exploration.result." + result.Kind.ToString().ToLowerInvariant() };
            if (result.Kind != ExplorationChoiceResultKind.Applied)
            {
                model.ShowsLockedReason = result.Kind == ExplorationChoiceResultKind.ConditionFailed;
                model.LockedReasonKey = result.LockedReasonKey;
                return model;
            }

            int rewardFacts = 0, vitalFacts = 0, other = 0, growth = 0, unknownStatuses = 0;
            string[] grants = Values(result.ItemGrantIds), spends = Values(result.ItemSpendIds);
            AddItems(model, grants, "exploration.effect.item-gain", itemNameKey, ref rewardFacts, ref other);
            AddItems(model, spends, "exploration.effect.item-spent", itemNameKey, ref rewardFacts, ref other);

            AddCount(model.Rewards, "exploration.effect.clues", Values(result.ClueIds).Length, ref rewardFacts);
            AddCount(model.Rewards, "exploration.effect.entries", Values(result.EncyclopediaEntryIds).Length, ref rewardFacts);
            AddCount(model.Rewards, "exploration.effect.careers", Values(result.CareerHintIds).Length, ref rewardFacts);
            AddCount(model.Rewards, "exploration.effect.quests", Values(result.FollowUpQuestIds).Length, ref rewardFacts);

            // 위에서 이미 한 줄로 센 결과는 '그 밖의 변화'로 또 세지 않는다.
            var represented = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in grants) if (TryItem(value, out string id, out _)) represented.Add("item-grant:" + id);
            foreach (string value in spends) if (TryItem(value, out string id, out _)) represented.Add("item-spend:" + id);
            AddRepresented(represented, "clue:", result.ClueIds);
            AddRepresented(represented, "encyclopedia:", result.EncyclopediaEntryIds);
            AddRepresented(represented, "career:", result.CareerHintIds);
            AddRepresented(represented, "quest:", result.FollowUpQuestIds);
            AddRepresented(represented, "status:", result.FatigueStatusIds);

            foreach (string outcome in Values(result.OutcomeIds))
            {
                int separator = outcome.IndexOf(':');
                string category = separator > 0 && separator < outcome.Length - 1 ? outcome.Substring(0, separator) : string.Empty;
                if (category == "trait" || category == "skill") { growth++; continue; }
                if (!represented.Contains(outcome)) other++;
            }
            AddCount(model.Rewards, "exploration.effect.growth", growth, ref rewardFacts);
            AddCount(model.Rewards, "exploration.effect.other", other, ref rewardFacts);

            foreach (string value in Values(result.FatigueStatusIds))
            {
                int separator = value.LastIndexOf(':');
                string category = separator > 0 ? value.Substring(0, separator) : string.Empty;
                if ((category == "energy" || category == "focus" || category == "stress")
                    && int.TryParse(value.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                {
                    if (amount == 0) continue;
                    model.Vitals.Add(new ExplorationEffect("exploration.effect." + category, null, amount, true));
                    vitalFacts++;
                }
                else unknownStatuses++;
            }
            AddCount(model.Vitals, "exploration.effect.status", unknownStatuses, ref vitalFacts);
            model.RewardFacts = rewardFacts;
            model.VitalFacts = vitalFacts;
            return model;
        }

        private static string[] Values(string[] values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>(values?.Length ?? 0);
            foreach (string v in values ?? Array.Empty<string>()) if (!string.IsNullOrWhiteSpace(v) && seen.Add(v)) list.Add(v);
            return list.ToArray();
        }

        private static void AddRepresented(HashSet<string> target, string prefix, string[] ids)
        {
            foreach (string id in Values(ids)) target.Add(prefix + id);
        }

        /// <summary>"물건id:수" 모양. 수는 1 이상의 정수여야 한다.</summary>
        private static bool TryItem(string value, out string id, out int count)
        {
            int separator = value.LastIndexOf(':');
            id = separator > 0 ? value.Substring(0, separator) : null;
            count = 0;
            return id != null && int.TryParse(value.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) && count > 0;
        }

        private static void AddCount(List<ExplorationEffect> target, string key, int count, ref int facts)
        {
            if (count <= 0) return;
            target.Add(new ExplorationEffect(key, null, count, false));
            facts += count;
        }

        private static void AddItems(ExplorationResultModel model, string[] values, string key, Func<string, string> itemNameKey, ref int facts, ref int malformed)
        {
            foreach (string value in values)
            {
                if (!TryItem(value, out string id, out int count)) { malformed++; continue; }
                model.Rewards.Add(new ExplorationEffect(key, itemNameKey?.Invoke(id) ?? string.Empty, count, false));
                facts++;
            }
        }
    }

    /// <summary>
    /// 탐색 선택 화면의 표시 모델: 지점 이름·설명 키와 선택지(이름 키, 미리보기 키, 고를 수 있는지, 잠긴 이유 키). 고를 수 있는지는 조건만 본다
    /// (위험·자원은 고를 때 Core가 본다). Core ExplorationSummaryBuilder가 만든다.
    /// </summary>
    public sealed class ExplorationModel
    {
        public static readonly ExplorationModel Empty = new ExplorationModel(new ExplorationSummaryModel(string.Empty, null, null, false, null));

        public ExplorationModel(ExplorationSummaryModel summary) { Summary = summary; }

        public ExplorationSummaryModel Summary { get; }
        public string InteractionId => Summary.InteractionId;
        public ExplorationChoiceSummaryModel[] Choices => Summary.Choices;

        public static ExplorationModel From(GameSession session, string interactionId)
        {
            if (session == null || string.IsNullOrEmpty(interactionId)) return Empty;
            var cache = new ExplorationLookupCache(session.Definitions.All<ExplorationInteraction>());
            var clues = new DiscoveryClueAccess(session.DiscoveryClues, new DiscoveryClueLookupCache(session.Definitions.All<DiscoveryClue>()));
            return new ExplorationModel(ExplorationSummaryBuilder.Build(interactionId, cache, session.Exploration, session.Student, session.Inventory,
                session.QuestLog, clues, session.StudentDay, session.StudentTimeMinutes));
        }
    }

    /// <summary>
    /// 탐색 선택(Unity ExplorationChoicePanel). 탐색 지점 상호작용(종류 "exploration")이 연다. 가운데 560x360 패널: 제목(지점 이름),
    /// 닫기, 선택지 버튼(이름 — 설명 한 줄, 조건이 안 되면 눌리지 않고 잠긴 이유를 보임), 결과 글.
    /// 선택지를 누르면 GameSession.ChooseExploration(비용·위험·보상·후속 퀘스트는 Core 실행기가 한 번에)을 부르고 결과를 아래에 보인다.
    /// 결과가 '그만둠'이면 닫는다. 결과 글이 칸을 넘으면 요약으로 줄인다(Unity 판과 같음). 선택지는 결과가 나와도 다시 만들지 않는다(Unity 판과 같음).
    /// Esc로 닫는다. 이동을 막는다(Unity: UiGameplayInputLock).
    /// </summary>
    public sealed class ExplorationScreen : CareerScreenBase
    {
        /// <summary>배치 — Unity ExplorationChoicePanel.BuildStaticUi 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 560f, Height = 360f;
            public const float TitleX = -20f, TitleY = 140f, TitleW = 420f, TitleH = 42f, TitleFont = 24f;
            public const float CloseX = 226f, CloseY = 142f, CloseW = 64f, CloseH = 32f, CloseFont = 16f;
            public const float ResultY = -116f, ResultW = 500f, ResultH = 72f, ResultFont = 16f;
            /// <summary>선택지 버튼들의 첫 버튼 가운데 y(Unity 목록 영역 가운데 20, 높이 190 → 위에서 19)와 줄 간격.</summary>
            public const float ChoiceFirstY = 96f, ChoiceStep = 46f, ChoiceW = 480f, ChoiceH = 38f, ChoiceFont = 15f;
            public static readonly UiColor Ink = UnityLayout.Color(0.22f, 0.19f, 0.18f);
        }

        public const string PanelId = "exploration";
        public const string TitleId = "exploration.title";
        public const string ResultId = "exploration.result";
        public const string CloseId = "exploration.close";
        public static string ChoiceId(int index) => "exploration.choice." + index;

        private PanelRoot _root;
        private UiLabel _title;
        private UiButton _close;
        private UiTextBlock _result;
        private readonly List<Common.MultilineButton> _choices = new List<Common.MultilineButton>(4);
        private ExplorationResultModel _lastResult;
        private bool _compactResult;

        public ExplorationScreen(ScreenServices services) : base(services) { }

        public ExplorationModel Model { get; private set; } = ExplorationModel.Empty;
        /// <summary>마지막으로 고른 선택지의 결과(고르기 전에는 null).</summary>
        public ExplorationChoiceResult? LastResult { get; private set; }
        /// <summary>결과 글(고르기 전에는 빈 글).</summary>
        public string ResultText => _result?.Text ?? string.Empty;

        protected override UiNode Build()
        {
            var root = new PanelRoot(this) { Id = PanelId };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _root = root;
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center });
            _title.Centered(Metrics.TitleX, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _close = root.Add(new UiButton { Id = CloseId, FontSize = Metrics.CloseFont });
            _close.Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseW, Metrics.CloseH);
            _close.Clicked += Close;
            _result = root.Add(new UiTextBlock { Id = ResultId, FontSize = Metrics.ResultFont, Color = Metrics.Ink });
            _result.Centered(0f, Metrics.ResultY, Metrics.ResultW, Metrics.ResultH);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            string interactionId = context.Interaction?.TargetId ?? context.Argument as string;
            Model = ExplorationModel.From(Services.Session, interactionId);
            LastResult = null;
            _lastResult = null;
            _compactResult = false;
            Show();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language) Show();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private void Show()
        {
            _title.Text = Known(Model.Summary.DisplayNameKey, "exploration.ui.title");
            _close.Text = T("facility.close");
            while (_choices.Count < Model.Choices.Length)
            {
                int index = _choices.Count;
                var button = _root.Add(new Common.MultilineButton { Id = ChoiceId(index), FontSize = Metrics.ChoiceFont });
                button.Centered(0f, Metrics.ChoiceFirstY - index * Metrics.ChoiceStep, Metrics.ChoiceW, Metrics.ChoiceH);
                button.Clicked += () => Choose(index);
                if (_root.Rect.W > 0) button.Layout(_root.Rect); // 이미 배치된 뒤에 만든 버튼도 바로 제자리에(다음 배치를 기다리지 않는다)
                _choices.Add(button);
            }
            for (int i = 0; i < _choices.Count; i++)
            {
                bool used = i < Model.Choices.Length;
                _choices[i].Visible = used;
                if (!used) continue;
                ExplorationChoiceSummaryModel choice = Model.Choices[i];
                _choices[i].Enabled = choice.Available;
                _choices[i].Text = ChoiceLabel(choice);
            }
            ShowResult();
        }

        private string ChoiceLabel(ExplorationChoiceSummaryModel choice)
        {
            string name = Known(choice.DisplayNameKey, "exploration.ui.choice");
            string detail = choice.Available ? Known(choice.PreviewHintKey, "exploration.ui.hint") : Known(choice.LockedReasonKey, "exploration.ui.locked");
            return FormatKnown("exploration.ui.choice-label", name, detail);
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= Model.Choices.Length || !Model.Choices[index].Available || Services.Commands == null) return;
            string interactionId = Model.InteractionId, choiceId = Model.Choices[index].ChoiceId;
            ExplorationChoiceResult result = Services.Commands.Run(s => s.ChooseExploration(interactionId, choiceId), ChangeTopics.All);
            LastResult = result;
            if (result.Kind == ExplorationChoiceResultKind.Dismissed) { Close(); return; }
            _lastResult = ExplorationResultModel.From(result, ItemNameKey);
            _compactResult = false;
            ShowResult();
        }

        /// <summary>물건 id → 이름 키. 같은 id가 하나가 아니면 null(Unity 판과 같음).</summary>
        private string ItemNameKey(string id)
        {
            if (Services.Session == null) return null;
            ItemDefinition found = null;
            int matches = 0;
            foreach (ItemDefinition item in Services.Session.Definitions.All<ItemDefinition>())
            {
                if (item == null || !string.Equals(item.Id, id, StringComparison.Ordinal)) continue;
                found = item;
                matches++;
            }
            return matches == 1 ? found.DisplayKey : null;
        }

        private void ShowResult()
        {
            _result.Text = _lastResult == null ? string.Empty : ResultLines(_lastResult, _compactResult);
        }

        private string ResultLines(ExplorationResultModel m, bool compact)
        {
            var lines = new List<string>(3) { Known(m.StatusKey, "exploration.result.updated") };
            if (m.ShowsLockedReason) lines.Add(Known(m.LockedReasonKey, "exploration.ui.locked"));
            if (m.Rewards.Count > 0) lines.Add(compact ? FormatKnown("exploration.effect.other", m.RewardFacts) : EffectsText(m.Rewards));
            if (m.Vitals.Count > 0) lines.Add(compact ? FormatKnown("exploration.effect.status", m.VitalFacts) : EffectsText(m.Vitals));
            return JoinLines(lines);
        }

        private string EffectsText(List<ExplorationEffect> effects)
        {
            var parts = new List<string>(effects.Count);
            foreach (ExplorationEffect e in effects)
            {
                if (e.NameKey != null) parts.Add(FormatKnown(e.Key, Known(e.NameKey, "exploration.effect.item"), e.Number));
                else if (e.Signed) parts.Add(FormatKnown(e.Key, e.Number.ToString("+0;-0;0", CultureInfo.InvariantCulture)));
                else parts.Add(FormatKnown(e.Key, e.Number));
            }
            return string.Join(" · ", parts);
        }

        /// <summary>결과 글이 칸을 넘으면 요약으로 줄인다(Unity: preferredHeight &gt; 칸 높이). 줄바꿈은 그릴 때 알 수 있어 그릴 때 본다.</summary>
        private void FitResult(ITextMeasurer text)
        {
            if (_lastResult == null || _compactResult) return;
            if (_result.ContentHeight(text) <= Metrics.ResultH + 0.5f) return;
            _compactResult = true;
            ShowResult();
        }

        private sealed class PanelRoot : UiNode
        {
            private readonly ExplorationScreen _owner;

            public PanelRoot(ExplorationScreen owner) { _owner = owner; Interactive = true; }

            public override void Emit(DrawList list, ITextMeasurer text)
            {
                _owner.FitResult(text);
                list.Panel(Rect, UiSkin.Panel, UiTheme.White);
            }
        }
    }
}
