using System.Collections.Generic;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Objectives.Sources
{
    /// <summary>
    /// 퀘스트(Unity QuestLogObjectiveSource): 퀘스트마다 한 줄. 끝낸 퀘스트는 상세에서 보상 받기(인벤토리 공간을 먼저 보고 전부 또는 하나도 안 준다,
    /// 받은 뒤 다시 눌러도 주지 않는다 — GameSession.ClaimQuestReward).
    /// </summary>
    public sealed class QuestLogSource : IObjectiveJournalSource
    {
        public string SourceId => "quest-log";
        public string CategoryId => JournalCategories.Quests;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            IReadOnlyList<QuestDefinition> quests = session.Definitions.All<QuestDefinition>();
            var items = new List<JournalItem>(quests.Count);
            foreach (QuestDefinition quest in quests)
            {
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                QuestStage state = session.QuestLog.GetState(quest);
                string questId = quest.Id;
                items.Add(new JournalItem("quest." + quest.Id, CategoryId, JournalSourceText.Safe(quest.DisplayNameKey, quest.Id), JournalText.Data(quest.DescriptionKey),
                    JournalSourceText.State(state == QuestStage.NotStarted ? "Available" : state.ToString()), Objectives(session.QuestLog, quest, state), Hint(state), false, Priority(state),
                    state == QuestStage.Completed ? new JournalAction(JournalText.Key("ui.journal.quest.claim"), session => Claim(session, questId)) : null));
            }
            return items;
        }

        private static JournalActionResult Claim(GameSession session, string questId)
        {
            bool claimed = session.ClaimQuestReward(questId);
            return new JournalActionResult(claimed, JournalText.Key(claimed ? "ui.journal.quest.claimed" : "ui.journal.quest.claim-failed"));
        }

        private static JournalText Hint(QuestStage state)
        {
            switch (state)
            {
                case QuestStage.NotStarted: return JournalText.Key("ui.journal.quest.accept-hint");
                case QuestStage.RewardClaimed: return JournalText.Key("ui.journal.quest.claimed-hint");
                case QuestStage.Completed: return JournalText.Key("ui.journal.quest.claim-hint");
                default: return JournalText.Key("ui.journal.quest.next-hint");
            }
        }

        private static int Priority(QuestStage state) =>
            state == QuestStage.Completed ? 10 : state == QuestStage.Active ? 20 : state == QuestStage.NotStarted ? 40 : 60;

        private static JournalText Objectives(QuestLog log, QuestDefinition quest, QuestStage state)
        {
            var lines = new List<JournalText>(quest.Objectives.Count);
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                QuestObjective objective = quest.Objectives[i];
                if (objective == null) continue;
                lines.Add(JournalSourceText.ObjectiveLine(JournalSourceText.ObjectiveLabel(objective.DisplayKey), log.GetObjectiveCount(quest, i), objective.RequiredCount));
            }
            return JournalSourceText.Lines(lines, state.ToString());
        }
    }

    /// <summary>
    /// 퀘스트 체인(Unity QuestChainObjectiveSource): 개발용이 아닌 체인마다 한 줄(지금 단계 이름·설명, 목표 진행, 다음 할 일).
    /// 다음 할 일: 완료 → 막힘 요약 → 목표를 다 채우고 기다리는 조건 → 첫 목표("다음: ...") → 체인 기록을 보라는 문구.
    /// </summary>
    public sealed class QuestChainSource : IObjectiveJournalSource
    {
        public string SourceId => "quest-chain-log";

        /// <summary>체인 한 줄의 안정 키(추적 지정에 저장되는 값). 체인 기록 화면의 추적 단추도 이 키로 추적한다.</summary>
        public static string StableKeyFor(string chainId) => "questchain." + chainId;
        public string CategoryId => JournalCategories.Quests;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            QuestChainLog log = session.QuestChains;
            IReadOnlyList<QuestChainDefinition> chains = session.Definitions.All<QuestChainDefinition>();
            var items = new List<JournalItem>(chains.Count);
            foreach (QuestChainDefinition chain in chains)
            {
                if (chain == null || chain.DeveloperOnly || string.IsNullOrEmpty(chain.Id)) continue;
                QuestChainState state = log.GetState(chain);
                QuestStepDefinition step = log.GetCurrentStep(chain);
                items.Add(new JournalItem(StableKeyFor(chain.Id), CategoryId,
                    JournalSourceText.Safe(step?.DisplayNameKey, chain.DisplayNameKey), JournalSourceText.Safe(step?.DescriptionKey, chain.DescriptionKey),
                    JournalSourceText.State(state.ToString()), Objectives(log, chain, step, state), Hint(log, chain, step, state), log.IsTracked(chain), Priority(chain, state)));
            }
            return items;
        }

        private static JournalText Objectives(QuestChainLog log, QuestChainDefinition chain, QuestStepDefinition step, QuestChainState state)
        {
            var lines = new List<JournalText>(4);
            if (step != null)
                for (int i = 0; i < step.Objectives.Count; i++)
                {
                    QuestObjective objective = step.Objectives[i];
                    if (objective != null)
                        lines.Add(JournalSourceText.ObjectiveLine(JournalSourceText.ObjectiveLabel(objective.DisplayKey), log.GetObjectiveProgress(chain, i), objective.RequiredCount));
                }
            return JournalSourceText.Lines(lines, state.ToString());
        }

        internal static JournalText Hint(QuestChainLog log, QuestChainDefinition chain, QuestStepDefinition step, QuestChainState state)
        {
            if (state == QuestChainState.Completed) return JournalText.Key("ui.journal.chain.completed-hint");
            if (state == QuestChainState.Blocked)
            {
                string summary = log.GetBlockedSummary(chain);
                return string.IsNullOrEmpty(summary) ? JournalText.Key("ui.journal.chain.blocked-hint") : JournalText.Data(summary);
            }
            if (step != null && step.CompletionConditions.Count > 0 && log.AreCurrentObjectivesComplete(chain) && step.CompletionConditions[0] != null)
                return JournalText.Data(step.CompletionConditions[0].BlockedSummary);
            QuestObjective first = step != null && step.Objectives.Count > 0 ? step.Objectives[0] : null;
            return first != null ? JournalText.Key("ui.journal.next", JournalSourceText.ObjectiveLabel(first.DisplayKey)) : JournalText.Key("ui.journal.chain.open-hint");
        }

        private static int Priority(QuestChainDefinition chain, QuestChainState state)
        {
            if (state == QuestChainState.Completed) return 10;
            if (state == QuestChainState.Tracked) return 20;
            if (state == QuestChainState.Active || state == QuestChainState.Paused) return 30;
            return chain.SortPriority == 0 ? 50 : chain.SortPriority;
        }
    }

    /// <summary>캠페인(Unity CampaignObjectiveSource): 오늘 캠페인 날 한 줄(주제, 방향, 날·고른 길 또는 고를 수 있는 길·끝낸 목표, 다음 안내).</summary>
    public sealed class CampaignSource : IObjectiveJournalSource
    {
        public string SourceId => "campaign-progress";
        public string CategoryId => JournalCategories.Campaign;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            CampaignProgress progress = session.Campaign;
            if (progress == null || !session.Definitions.TryGet(progress.ActiveCampaignId ?? string.Empty, out Campaign campaign)) return System.Array.Empty<JournalItem>();
            // Unity는 여기서 진행의 날 번호를 학생 날로 맞췄다. 화면은 상태를 바꾸지 않으므로 학생 날로 그 날을 찾기만 한다.
            CampaignDay day = campaign.GetDayByNumber(session.StudentDay);
            if (day == null) return System.Array.Empty<JournalItem>();
            return new[]
            {
                new JournalItem("campaign." + campaign.Id + ".day." + day.DayNumber, CategoryId, JournalSourceText.Safe(day.ThemeKey, campaign.DescriptionKey),
                    JournalSourceText.Safe(day.DirectionText, campaign.DescriptionKey), JournalSourceText.State("Active"), Progress(progress, day), JournalText.Data(day.NextGuideText), false, 0),
            };
        }

        private static JournalText Progress(CampaignProgress progress, CampaignDay day)
        {
            var parts = new List<JournalText> { JournalText.Key("ui.journal.campaign.day", day.DayNumber) };
            string selected = progress.GetSelectedRouteId(day.Id);
            CampaignRoute route = null;
            foreach (CampaignRoute r in day.Routes) if (r != null && r.Id == selected && !string.IsNullOrEmpty(selected)) route = r;
            if (route != null) parts.Add(JournalText.Key("ui.journal.campaign.route-item", JournalSourceText.Safe(route.DisplayNameKey, route.Id)));
            else
            {
                var names = new List<JournalText>(day.Routes.Count);
                foreach (CampaignRoute r in day.Routes) if (r != null) names.Add(JournalSourceText.Safe(r.DisplayNameKey, r.Id));
                if (names.Count > 0) parts.Add(JournalText.Key("ui.journal.campaign.routes-item", JournalText.List(names, "ui.journal.comma-separator")));
            }
            string[] completed = progress.LastProgressLogIds;
            if (completed.Length > 0)
            {
                var labels = new List<JournalText>(completed.Length);
                foreach (string id in completed) labels.Add(JournalText.KeyOr("ui.journal.campaign.done." + id, id));
                parts.Add(JournalText.Key("ui.journal.campaign.completed-item", JournalText.List(labels, "ui.journal.comma-separator")));
            }
            return JournalText.List(parts);
        }
    }
}
