using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    public sealed class QuestChainObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Quests", "Quests");

        public string SourceId => "quest-chain-log";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var chainPanel = UnityEngine.Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
            var chains = chainPanel != null ? chainPanel.Chains : null;
            var log = chainPanel != null ? chainPanel.Log : null;
            if (chains == null || log == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < chains.Length; i++)
            {
                var chain = chains[i];
                if (chain == null || chain.DeveloperOnly || string.IsNullOrEmpty(chain.Id))
                {
                    continue;
                }

                var state = log.GetState(chain);
                var currentStep = log.GetCurrentStep(chain);
                items.Add(new ObjectiveJournalItem(
                    "questchain." + chain.Id,
                    Category.CategoryId,
                    ObjectiveJournalSourceText.SafeText(currentStep != null ? currentStep.DisplayNameKey : chain.DisplayNameKey, chain.DisplayNameKey),
                    ObjectiveJournalSourceText.SafeText(currentStep != null ? currentStep.DescriptionKey : chain.DescriptionKey, chain.DescriptionKey),
                    ObjectiveJournalSourceText.State(state.ToString()),
                    BuildQuestChainObjectiveText(log, chain),
                    BuildQuestChainHintText(log, chain, state),
                    log.IsTracked(chain),
                    BuildQuestChainSortPriority(chain, state)));
            }

            return items.ToArray();
        }

        private static string BuildQuestChainObjectiveText(QuestChainLog log, QuestChainDefinition chain)
        {
            var step = log.GetCurrentStep(chain);
            var objectives = step != null ? step.Objectives : null;
            if (objectives == null || objectives.Length == 0)
            {
                return chain != null ? ObjectiveJournalSourceText.State(log.GetState(chain).ToString()) : string.Empty;
            }

            string text = string.Empty;
            for (int i = 0; i < objectives.Length; i++)
            {
                var objective = objectives[i];
                if (objective == null)
                {
                    continue;
                }

                string line = ObjectiveJournalSourceText.ObjectiveLabel(objective.DisplayKey) + " " + log.GetObjectiveProgress(chain, i) + " / " + objective.RequiredCount;
                text += string.IsNullOrEmpty(text) ? line : " | " + line;
            }

            return string.IsNullOrEmpty(text) ? ObjectiveJournalSourceText.State(log.GetState(chain).ToString()) : text;
        }

        private static string BuildQuestChainHintText(QuestChainLog log, QuestChainDefinition chain, QuestChainState state)
        {
            if (state == QuestChainState.Completed)
            {
                return ObjectiveJournalSourceText.Localized("ui.journal.chain.completed-hint", "Claim rewards or review the next route.");
            }

            if (state == QuestChainState.Blocked)
            {
                string summary = log.GetBlockedSummary(chain);
                return string.IsNullOrEmpty(summary) ? ObjectiveJournalSourceText.Localized("ui.journal.chain.blocked-hint", "Review the blocked quest-chain condition.") : summary;
            }

            var step = log.GetCurrentStep(chain);
            if (step != null && step.CompletionConditions != null && step.CompletionConditions.Length > 0 && log.AreCurrentObjectivesComplete(chain))
            {
                var waiting = step.CompletionConditions[0];
                if (waiting != null) return ObjectiveJournalSourceText.SafeText(waiting.BlockedSummary, waiting.BlockedSummary);
            }
            var objective = step != null && step.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
            return objective != null ? ObjectiveJournalSourceText.LocalizedFormat("ui.journal.next", "Next: {0}", ObjectiveJournalSourceText.ObjectiveLabel(objective.DisplayKey)) : ObjectiveJournalSourceText.Localized("ui.journal.chain.open-hint", "Open quest chain log.");
        }

        private static int BuildQuestChainSortPriority(QuestChainDefinition chain, QuestChainState state)
        {
            if (state == QuestChainState.Completed)
            {
                return 10;
            }

            if (state == QuestChainState.Tracked)
            {
                return 20;
            }

            if (state == QuestChainState.Active || state == QuestChainState.Paused)
            {
                return 30;
            }

            int priority = chain != null ? chain.SortPriority : 0;
            return priority == 0 ? 50 : priority;
        }
    }
}
