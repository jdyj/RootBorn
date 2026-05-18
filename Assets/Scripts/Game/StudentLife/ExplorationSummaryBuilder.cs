using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;

namespace Rootborn.Game.StudentLife
{
    public sealed class ExplorationLookupCache
    {
        private readonly Dictionary<string, ExplorationInteractionDefinition> _interactions = new Dictionary<string, ExplorationInteractionDefinition>(StringComparer.Ordinal);
        public int LookupCount { get; private set; }

        public ExplorationLookupCache(IEnumerable<ExplorationInteractionDefinition> interactions)
        {
            if (interactions == null) return;
            foreach (var interaction in interactions)
            {
                if (interaction != null && !string.IsNullOrEmpty(interaction.Id)) _interactions[interaction.Id] = interaction;
            }
        }

        public bool TryGetInteraction(string interactionId, out ExplorationInteractionDefinition interaction)
        {
            LookupCount++;
            return _interactions.TryGetValue(string.IsNullOrEmpty(interactionId) ? string.Empty : interactionId, out interaction);
        }
    }

    public readonly struct ExplorationChoiceSummaryModel
    {
        public readonly string ChoiceId;
        public readonly string DisplayNameKey;
        public readonly string PreviewHintKey;
        public readonly bool Available;
        public readonly string LockedReasonKey;

        public ExplorationChoiceSummaryModel(string choiceId, string displayNameKey, string previewHintKey, bool available, string lockedReasonKey)
        {
            ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? ChoiceId : displayNameKey;
            PreviewHintKey = string.IsNullOrEmpty(previewHintKey) ? DisplayNameKey : previewHintKey;
            Available = available;
            LockedReasonKey = string.IsNullOrEmpty(lockedReasonKey) ? string.Empty : lockedReasonKey;
        }
    }

    public readonly struct ExplorationSummaryModel
    {
        public readonly string InteractionId;
        public readonly string DisplayNameKey;
        public readonly string DescriptionKey;
        public readonly bool Completed;
        public readonly ExplorationChoiceSummaryModel[] Choices;

        public ExplorationSummaryModel(string interactionId, string displayNameKey, string descriptionKey, bool completed, ExplorationChoiceSummaryModel[] choices)
        {
            InteractionId = string.IsNullOrEmpty(interactionId) ? string.Empty : interactionId;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? InteractionId : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            Completed = completed;
            Choices = choices ?? Array.Empty<ExplorationChoiceSummaryModel>();
        }
    }

    public static class ExplorationSummaryBuilder
    {
        public static ExplorationSummaryModel Build(string interactionId, ExplorationLookupCache cache, ExplorationProgress progress, StudentLifeProgress student, Inventory inventory, QuestLog questLog, DiscoveryClueProgress clueProgress, int day, int timeMinutes)
        {
            if (cache == null || !cache.TryGetInteraction(interactionId, out var interaction) || interaction == null)
            {
                return new ExplorationSummaryModel(interactionId, interactionId, interactionId, false, Array.Empty<ExplorationChoiceSummaryModel>());
            }

            var context = new ExplorationInteractionContext(progress, student, inventory, questLog, clueProgress, null, day, timeMinutes);
            var choices = new List<ExplorationChoiceSummaryModel>(interaction.Choices.Count);
            for (int i = 0; i < interaction.Choices.Count; i++)
            {
                var choice = interaction.Choices[i];
                if (choice == null) continue;
                bool available = choice.ConditionsSatisfied(context, out string reason);
                choices.Add(new ExplorationChoiceSummaryModel(choice.Id, choice.DisplayNameKey, choice.PreviewHintKey, available, reason));
            }

            bool completed = progress != null && progress.GetRecord(interaction.Id).Completed;
            return new ExplorationSummaryModel(interaction.Id, interaction.DisplayNameKey, interaction.DescriptionKey, completed, choices.ToArray());
        }
    }
}
